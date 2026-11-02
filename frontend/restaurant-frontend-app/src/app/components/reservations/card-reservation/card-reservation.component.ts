import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap } from 'rxjs';
import { Router } from '@angular/router';

import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';

import { CreateOrderRequest } from '@app/models/cart/create-order-request.model';
import {
  ReservationCustomer,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { PlaceOrderWaiterComponent } from '@app/components/place-order-waiter/place-order-waiter.component';
import { Roles } from '@app/shared/constants/roles';
import { buildPreOrderQueryParams } from '@app/shared/helpers/pre-order-query-params';
import { GroupOrderPageComponent } from '@app/pages/group-order-page/group-order-page.component';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { CustomSelectComponent } from '@app/shared/custom-components/custom-select/custom-select.component';

interface ReservationStatusView {
  class: string;
  label: string;
}

interface WaiterPreOrderNavigationData {
  reservationId: string;
  createdByName: string;
  createdByRole: string;
  tableName: string;
}

@Component({
  selector: 'app-card-reservation',
  standalone: true,
  imports: [
    DatePipe,
    CustomSelectComponent,
    PlaceOrderWaiterComponent,
    ModalComponent,
    GroupOrderPageComponent,
  ],
  templateUrl: './card-reservation.component.html',
  styleUrl: './card-reservation.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardReservationComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly groupOrders = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  readonly Roles = Roles;
  readonly ReservationStatusCustomer = ReservationStatusCustomer;

  readonly r = input.required<ReservationCustomer>();
  readonly role = input<string>(Roles.CUSTOMER);
  readonly statusEnum =
    input<Record<number, ReservationStatusView>>({});

  readonly availableTableOptions = input<string[]>([]);

  readonly availableTableOptionsMap =
    input<Map<string, string>>(new Map<string, string>());

  readonly selectedTableLabel = input('');

  readonly createdByName = input<string | undefined>();

  readonly createdByRole =
    input<ReservationCustomer['createdByRole']>();
  readonly tableChanged = output<{
    reservationId: string;
    newTableId: string;
  }>();

  readonly cancelReservation = output<string>();

  readonly editReservation =
    output<ReservationCustomer>();

  readonly postponeReservation =
    output<ReservationCustomer>();

  readonly leaveFeedbackReservation =
    output<ReservationCustomer>();

  readonly updateFeedbackReservation =
    output<ReservationCustomer>();

  readonly sendAnonymousFeedback =
    output<ReservationCustomer>();

  readonly statusChanged = output<{
    id: string;
    newStatus: ReservationStatusCustomer;
  }>();

  readonly preOrder = output<CreateOrderRequest>();
  readonly showWaiterOrderModal = signal(false);

  readonly statusView = computed<ReservationStatusView>(() => {
    const reservation = this.r();

    return (
      this.statusEnum()[reservation.status] ?? {
        class: '',
        label: 'Unknown',
      }
    );
  });

  readonly displayedTableLabel = computed(() => {
    return this.selectedTableLabel() || 'Table (Unknown)';
  });

  readonly creatorName = computed(() => {
    return (
      this.createdByName() ||
      this.r().createdByName ||
      'Unknown'
    );
  });

  readonly creatorRole = computed(() => {
    return (
      this.createdByRole() ||
      this.r().createdByRole ||
      Roles.CUSTOMER
    );
  });

  readonly preOrderItemCount = computed(() => {
    return this.r().preOrder?.orderItems?.length ?? 0;
  });

  readonly canOpenWaiterPreOrder = computed(() => {
    return (
      this.preOrderItemCount() > 0 ||
      this.creatorRole() === Roles.WAITER
    );
  });

  readonly isAnonymous = computed(() => {
    return !this.r().customerId;
  });

  readonly hasFullFeedback = computed(() => {
    const feedback = this.r().feedback;

    return (
      feedback?.cuisineExperience != null &&
      feedback?.service != null
    );
  });

  onPreOrder(): void {
    void this.router.navigate(['/menu'], {
      queryParams: buildPreOrderQueryParams(this.r()),
    });
  }

  readonly groupOrderOpen = signal(false);

  readonly inviteOpen = signal(false);
  readonly inviteLink = signal<string | null>(null);
  readonly inviteLoading = signal(false);

  readonly bandLabel = computed(() => {
    const label = this.statusView().label;
    return label === 'Finished' ? 'Completed' : label;
  });

  readonly hasCustomerActions = computed(
    () => this.r().status !== ReservationStatusCustomer.Cancelled,
  );

  /** Submitted guests' carts of the group: how many people sent a pre-order and what exactly. */
  private readonly groupSnapshot = signal<{ submittedGuests: number; signature: string } | null>(null);
  private readonly seenSignature = signal('');

  readonly showPreOrders = computed(
    () => this.role() === Roles.CUSTOMER && this.r().guestCount > 1,
  );

  readonly preOrdersCount = computed(() => {
    const reservation = this.r();
    const ownerOrdered =
      (reservation.preOrder?.orderItems?.length ?? 0) > 0 || !!reservation.hasSubmittedPreOrder;
    const snapshot = this.groupSnapshot();

    if (snapshot) {
      return Math.min(reservation.guestCount, snapshot.submittedGuests + (ownerOrdered ? 1 : 0));
    }

    return reservation.preOrders?.length ?? (ownerOrdered ? 1 : 0);
  });

  /** Orange dot: a guest submitted or changed a pre-order that the owner has not looked at yet. */
  readonly hasUpdates = computed(() => {
    const snapshot = this.groupSnapshot();

    return (
      this.r().status === ReservationStatusCustomer.Reserved &&
      !!snapshot &&
      snapshot.signature !== '' &&
      snapshot.signature !== this.seenSignature()
    );
  });

  private get seenKey(): string {
    return `group-preorder-seen:${this.r().id}`;
  }

  ngOnInit(): void {
    if (
      this.role() !== Roles.CUSTOMER ||
      this.r().status !== ReservationStatusCustomer.Reserved ||
      this.r().guestCount < 2
    ) {
      return;
    }

    try {
      this.seenSignature.set(localStorage.getItem(this.seenKey) ?? '');
    } catch {
      // storage is optional
    }

    this.groupOrders
      .getInviteToken$(this.r().id, { silent: true })
      .pipe(
        switchMap(({ groupReservationId }) =>
          groupReservationId
            ? this.groupOrders.getSessions$(groupReservationId, { silent: true })
            : of(null),
        ),
        catchError(() => of(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((snapshot) => {
        if (!snapshot) {
          return;
        }

        const guests = snapshot.participants.filter(
          (person) => person.type !== 'Owner' && person.isSubmitted && person.items.length > 0,
        );

        this.groupSnapshot.set({
          submittedGuests: guests.length,
          signature: guests.map((guest) => `${guest.id}:${guest.total}`).join('|'),
        });
      });
  }

  private markUpdatesSeen(): void {
    const signature = this.groupSnapshot()?.signature ?? '';
    this.seenSignature.set(signature);

    try {
      localStorage.setItem(this.seenKey, signature);
    } catch {
      // storage is optional
    }
  }

  openInvite(): void {
    this.inviteOpen.set(true);
    if (this.inviteLink() || this.inviteLoading()) {
      return;
    }

    this.inviteLoading.set(true);
    this.groupOrders.getInviteToken$(this.r().id).subscribe({
      next: ({ token }) => {
        this.inviteLink.set(`${window.location.origin}/join/${token}`);
        this.inviteLoading.set(false);
      },
      error: () => this.inviteLoading.set(false),
    });
  }

  closeInvite(): void {
    this.inviteOpen.set(false);
  }

  copyInvite(): void {
    const link = this.inviteLink();
    if (!link) {
      return;
    }

    navigator.clipboard
      .writeText(link)
      .then(() => this.toast.success('Link copied! Share it with your friends.'))
      .catch(() => this.toast.error('Could not copy the link. Select it and copy manually.'));
  }

  onGroupPreOrder(): void {
    this.markUpdatesSeen();
    this.groupOrderOpen.set(true);
  }

  closeGroupOrder(): void {
    this.groupOrderOpen.set(false);
  }

  onPreOrderWaiter(): void {
    if (this.preOrderItemCount() > 0) {
      this.navigateToWaiterPreOrder();
      return;
    }

    if (this.creatorRole() === Roles.WAITER) {
      this.showWaiterOrderModal.set(true);
    }
  }

  closeWaiterOrderModal(): void {
    this.showWaiterOrderModal.set(false);
  }

  onWaiterOrderCreated(order: CreateOrderRequest): void {
    this.showWaiterOrderModal.set(false);
    this.preOrder.emit(order);
  }

  onTableChange(label: string): void {
    const reservation = this.r();
    const newTableId =
      this.availableTableOptionsMap().get(label);

    if (!newTableId || newTableId === reservation.tableId) {
      return;
    }

    this.tableChanged.emit({
      reservationId: reservation.id,
      newTableId,
    });
  }

  onStatusChange(
    newStatus: ReservationStatusCustomer,
  ): void {
    this.statusChanged.emit({
      id: this.r().id,
      newStatus,
    });
  }

  private navigateToWaiterPreOrder(): void {
    const reservation = this.r();

    const navigationData: WaiterPreOrderNavigationData = {
      reservationId: reservation.id,
      createdByName: this.creatorName(),
      createdByRole: this.creatorRole(),
      tableName: reservation.tableName || 'Unknown table',
    };

    sessionStorage.setItem(
      'waiterPreOrderData',
      JSON.stringify(navigationData),
    );

    void this.router.navigate(['/waiter-pre-order'], {
      state: navigationData,
    });
  }
}
