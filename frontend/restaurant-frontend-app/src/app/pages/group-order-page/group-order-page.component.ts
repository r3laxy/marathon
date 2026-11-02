import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { EMPTY, Observable, catchError, exhaustMap, filter, forkJoin, interval, map, merge, of, switchMap, takeWhile } from 'rxjs';

import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { Participant, ParticipantItem } from '@app/models/group-orders/participant.model';
import { Order } from '@app/models/orders/order.model';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';

const SYNC_INTERVAL_MS = 15_000;

type LoadState = 'loading' | 'ready' | 'notFound' | 'error';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, ModalComponent, CurrencyPipe, DatePipe],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly groupOrders = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly http = inject(HttpClient, { optional: true });
  private readonly baseUrl = inject(API_BASE_URL, { optional: true });
  /** Власні страви Owner живуть у звичайному pre-order (його показує й корзина), а не в owner-сесії. */
  private ownerItems: ParticipantItem[] | null = null;
  private readonly routeReservationId = inject(ActivatedRoute).snapshot.paramMap.get('reservationId') ?? '';

  /** Задається, коли екран відкрито модальним вікном з картки бронювання; інакше id береться з адреси. */
  readonly reservationIdInput = input('', { alias: 'reservationId' });
  readonly embedded = computed(() => this.reservationIdInput() !== '');

  private get reservationId(): string {
    return this.reservationIdInput() || this.routeReservationId;
  }

  readonly loadState = signal<LoadState>('loading');
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal('');
  readonly status = signal<GroupStatusResponse | null>(null);

  readonly sentLocally = signal(false);
  readonly submitting = signal(false);
  readonly submitDialogOpen = signal(false);
  readonly guestToRemove = signal<Participant | null>(null);
  readonly removing = signal(false);

  readonly updatedAt = signal<Date | null>(null);
  readonly refreshFailed = signal(false);
  private readonly refreshRequests = signal(0);

  readonly groupId = computed(() => this.invite()?.GroupReservationId ?? '');
  readonly isSent = computed(() => this.sentLocally() || this.status()?.status === 'Submitted');
  readonly isClosed = computed(() => this.status()?.status === 'Closed');
  readonly isReadOnly = computed(() => this.isSent() || this.isClosed());

  readonly statusLabel = computed(() => {
    if (this.isSent()) return 'Sent to Restaurant';
    if (this.isClosed()) return 'Closed';
    return 'Collecting pre-orders';
  });

  readonly maxGuests = computed(() => this.status()?.maxCapacity ?? this.invite()?.MaxGuestCount ?? this.participants().length);
  readonly guestsLabel = computed(() => `${this.participants().length} of ${this.maxGuests()} guests`);
  /** Усі місця зайняті — щоб покликати ще когось, треба збільшити кількість гостей у бронюванні. */
  readonly isFull = computed(() => this.participants().length >= this.maxGuests());

  readonly inviteDialogOpen = signal(false);
  readonly inviteLink = signal<string | null>(null);
  readonly inviteLoading = signal(false);

  /** Лише підтверджені кошики, однакові страви — одним рядком. API-total не використовуємо: він рахує всіх. */
  readonly consolidated = computed(() => consolidatePreOrder(this.participants()));
  readonly pendingCount = computed(() => this.participants().filter((person) => !person.isSubmitted).length);
  readonly hasAnyDishes = computed(() => this.participants().some((person) => person.items.length > 0));
  readonly canSubmit = computed(
    () => !this.isReadOnly() && !this.submitting() && this.consolidated().items.length > 0,
  );

  constructor() {
    this.startSync();
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loadState.set('loading');
    this.groupOrders
      .getGroupInvite$(this.reservationId)
      .pipe(
        switchMap((invite) =>
          // No group id: the group was already sent, so show the owner's order read-only.
          invite.GroupReservationId === ''
            ? this.loadSentFallback$(invite)
            : forkJoin({
            invite: of(invite),
            sessions: this.groupOrders.getSessions$(invite.GroupReservationId),
            ownerSessionId: this.groupOrders.getOwnerSessionId$(this.reservationId),
            status: this.groupOrders.getStatus$(invite.GroupReservationId),
            ownerItems: this.loadOwnerItems$(),
          }),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ invite, sessions, ownerSessionId, status, ownerItems }) => {
          this.invite.set(invite);
          this.ownerItems = ownerItems;
          this.ownerSessionId.set(ownerSessionId);
          this.status.set(status);
          this.participants.set(this.withOwnerOrder(sessions.participants));
          this.updatedAt.set(new Date());
          this.refreshFailed.set(false);
          this.loadState.set('ready');
        },
        error: (error: unknown) => {
          this.loadState.set(error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error');
        },
      });
  }

  private loadSentFallback$(invite: GuestInvite) {
    return this.loadOwnerItems$().pipe(
      map((ownerItems) => ({
        invite,
        sessions: {
          participants: [
            {
              id: 'owner',
              type: 'Owner' as const,
              name: 'Owner',
              items: ownerItems ?? [],
              total: (ownerItems ?? []).reduce((sum, item) => sum + item.dish.price * item.quantity, 0),
              isSubmitted: true,
            },
          ],
          total: 0,
        },
        ownerSessionId: 'owner',
        status: { currentParticipants: 1, maxCapacity: invite.MaxGuestCount, status: 'Submitted' as const },
        ownerItems: null as ParticipantItem[] | null,
      })),
    );
  }

  refresh(): void {
    this.refreshRequests.update((count) => count + 1);
  }

  openSubmitDialog(): void {
    if (this.canSubmit()) this.submitDialogOpen.set(true);
  }

  closeSubmitDialog(): void {
    if (!this.submitting()) this.submitDialogOpen.set(false);
  }

  confirmSubmit(): void {
    if (this.submitting() || !this.canSubmit()) return;
    this.submitting.set(true);
    this.groupOrders.submit$(this.groupId(), this.reservationId).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitDialogOpen.set(false);
        this.sentLocally.set(true);
        this.toast.success('Pre-order sent to the restaurant');
      },
      // toast з помилкою вже показує tokenInterceptor
      error: () => {
        this.submitting.set(false);
        this.submitDialogOpen.set(false);
      },
    });
  }

  openInviteDialog(): void {
    this.inviteDialogOpen.set(true);
    if (this.inviteLink() || this.inviteLoading()) return;

    this.inviteLoading.set(true);
    this.groupOrders.getInviteToken$(this.reservationId).subscribe({
      next: ({ token }) => {
        this.inviteLink.set(`${window.location.origin}/join/${token}`);
        this.inviteLoading.set(false);
      },
      error: () => this.inviteLoading.set(false),
    });
  }

  closeInviteDialog(): void {
    this.inviteDialogOpen.set(false);
  }

  copyInviteLink(): void {
    const link = this.inviteLink();
    if (!link) return;

    navigator.clipboard
      .writeText(link)
      .then(() => this.toast.success('Invite link copied'))
      .catch(() => this.toast.error('Could not copy the link. Select it and copy manually.'));
  }

  askRemove(person: Participant): void {
    this.guestToRemove.set(person);
  }

  cancelRemove(): void {
    if (!this.removing()) this.guestToRemove.set(null);
  }

  confirmRemove(): void {
    const person = this.guestToRemove();
    if (!person || this.removing()) return;
    this.removing.set(true);
    this.groupOrders.removeSession$(this.groupId(), person.id).subscribe({
      next: (snapshot) => {
        this.participants.set(this.withOwnerOrder(snapshot.participants));
        this.removing.set(false);
        this.guestToRemove.set(null);
      },
      error: () => {
        this.removing.set(false);
        this.guestToRemove.set(null);
      },
    });
  }

  /**
   * Один потік на весь життєвий цикл сторінки: таймер 15 с + кнопка Refresh.
   * exhaustMap — поки попередній запит не відповів, новий не стартує.
   * Після submit потік завершується; при виході зі сторінки — takeUntilDestroyed.
   */
  private startSync(): void {
    const manualRefresh$ = toObservable(this.refreshRequests).pipe(filter((count) => count > 0));

    merge(interval(SYNC_INTERVAL_MS), manualRefresh$)
      .pipe(
        takeWhile(() => !this.isReadOnly()),
        filter(() => this.loadState() === 'ready'),
        exhaustMap(() =>
          forkJoin({
            sessions: this.groupOrders.getSessions$(this.groupId(), { silent: true }),
            status: this.groupOrders.getStatus$(this.groupId(), { silent: true }),
            ownerItems: this.loadOwnerItems$(),
          }).pipe(
            catchError(() => {
              this.refreshFailed.set(true);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ sessions, status, ownerItems }) => {
        this.ownerItems = ownerItems;
        this.status.set(status);
        this.participants.set(this.withOwnerOrder(sessions.participants));
        this.updatedAt.set(new Date());
        this.refreshFailed.set(false);
      });
  }

  /** Звичайний pre-order Owner (той самий, що в корзині). Помилка — не привід ламати екран. */
  private loadOwnerItems$(): Observable<ParticipantItem[] | null> {
    if (!this.http || !this.baseUrl) return of(null);

    return this.http
      .get<Order>(`${this.baseUrl}/orders/${encodeURIComponent(this.reservationId)}`, {
        context: new HttpContext().set(SKIP_ERROR_TOAST, true),
      })
      .pipe(
        map((order) =>
          (order.orderItems ?? []).map(
            (item): ParticipantItem => ({
              dish: { id: item.dish.id, name: item.dish.name, price: item.dish.price, imageUrl: item.dish.imageUrl },
              quantity: item.quantity,
            }),
          ),
        ),
        catchError(() => of(null)),
      );
  }

  /**
   * Підставляє власні страви Owner зі звичайного pre-order.
   * Після відправки групи ресторан отримав у цей pre-order ще й страви гостей — їх віднімаємо назад.
   */
  private withOwnerOrder(participants: Participant[]): Participant[] {
    const ownerItems = this.ownerItems;
    if (!ownerItems) return participants;

    let items = ownerItems;
    if (this.isSent()) {
      const guestQuantity = new Map<string, number>();
      for (const guest of participants.filter((person) => person.type !== 'Owner' && person.isSubmitted)) {
        for (const item of guest.items) {
          guestQuantity.set(item.dish.id, (guestQuantity.get(item.dish.id) ?? 0) + item.quantity);
        }
      }
      items = ownerItems
        .map((item) => ({ ...item, quantity: item.quantity - (guestQuantity.get(item.dish.id) ?? 0) }))
        .filter((item) => item.quantity > 0);
    }

    const total = items.reduce((sum, item) => sum + item.dish.price * item.quantity, 0);
    return participants.map((person) =>
      person.type === 'Owner' ? { ...person, items, total, isSubmitted: person.isSubmitted || items.length > 0 } : person,
    );
  }
}
