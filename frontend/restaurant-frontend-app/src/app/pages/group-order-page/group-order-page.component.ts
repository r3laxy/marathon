import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { EMPTY, catchError, exhaustMap, filter, forkJoin, interval, merge, of, switchMap, takeWhile } from 'rxjs';

import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
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
  private readonly reservationId = inject(ActivatedRoute).snapshot.paramMap.get('reservationId') ?? '';

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

  readonly guestsLabel = computed(() => {
    const current = this.status()?.currentParticipants ?? this.participants().length;
    const max = this.status()?.maxCapacity ?? this.invite()?.MaxGuestCount ?? current;
    return `${current} of ${max} guests`;
  });

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
          forkJoin({
            invite: of(invite),
            sessions: this.groupOrders.getSessions$(invite.GroupReservationId),
            ownerSessionId: this.groupOrders.getOwnerSessionId$(this.reservationId),
            status: this.groupOrders.getStatus$(invite.GroupReservationId),
          }),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ invite, sessions, ownerSessionId, status }) => {
          this.invite.set(invite);
          this.participants.set(sessions.participants);
          this.ownerSessionId.set(ownerSessionId);
          this.status.set(status);
          this.updatedAt.set(new Date());
          this.refreshFailed.set(false);
          this.loadState.set('ready');
        },
        error: (error: unknown) => {
          this.loadState.set(error instanceof HttpErrorResponse && error.status === 404 ? 'notFound' : 'error');
        },
      });
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
        this.participants.set(snapshot.participants);
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
          }).pipe(
            catchError(() => {
              this.refreshFailed.set(true);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(({ sessions, status }) => {
        this.participants.set(sessions.participants);
        this.status.set(status);
        this.updatedAt.set(new Date());
        this.refreshFailed.set(false);
      });
  }
}
