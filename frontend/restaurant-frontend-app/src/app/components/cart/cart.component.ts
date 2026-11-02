import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { forkJoin, map, Observable } from 'rxjs';

import { GroupPreOrder } from '@app/models/orders/group-pre-order.model';
import { Order } from '@app/models/orders/order.model';
import { GroupPreOrderService } from '@app/services/pre-orders/group-pre-order.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { getServerErrorMessage } from '@app/shared/helpers/http-error-helpers';

import { CancelOrderModalComponent } from './components/cancel-order-modal/cancel-order-modal.component';
import { NotSubmittedOrderComponent } from './components/not-submitted-order/not-submitted-order.component';
import { SubmitOrderModalComponent } from './components/submit-order-modal/submit-order-modal.component';
import { SubmittedOrderComponent } from './components/submitted-order/submitted-order.component';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [
    SubmittedOrderComponent,
    NotSubmittedOrderComponent,
    CancelOrderModalComponent,
    SubmitOrderModalComponent,
  ],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CartComponent implements OnInit {
  private readonly preOrdersService =
    inject(PreOrdersService);

  private readonly groupPreOrderService = inject(GroupPreOrderService);
  private readonly toastService = inject(ToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly closeCartEvent = output<void>();

  readonly orders = signal<Order[]>([]);

  /** Group pre-orders (guests' dishes) of not submitted orders, keyed by reservation id. */
  readonly groups = signal<Record<string, GroupPreOrder>>({});

  readonly cancelOrderId = signal<string | null>(null);
  readonly submitOrderId = signal<string | null>(null);

  readonly submittedOrders = computed(() =>
    this.orders().filter(
      (order) => order.status === 1 || order.status === 3,
    ),
  );

  readonly unsubmittedOrders = computed(() =>
    this.orders().filter((order) => order.status === 0),
  );

  readonly submittedOrder = computed(() => {
    const orderId = this.submitOrderId();

    if (!orderId) {
      return undefined;
    }

    return this.orders().find(
      (order) => order.orderId === orderId,
    );
  });

  ngOnInit(): void {
    this.loadOrders();
  }

  closeCart(): void {
    this.closeCartEvent.emit();
  }

  openCancelModal(orderId: string): void {
    this.cancelOrderId.set(orderId);
    this.submitOrderId.set(null);
  }

  closeCancelModal(): void {
    this.cancelOrderId.set(null);
  }

  closeSubmitModal(): void {
    this.submitOrderId.set(null);
  }

  editOrderModal(): void {
    const orderId = this.submitOrderId();

    if (!orderId) {
      console.error('No order selected for editing');
      return;
    }

    this.editOrder(orderId);
    this.closeSubmitModal();
  }

  goToReservations(): void {
    this.closeCart();
    void this.router.navigate(['/reservations']);
  }

  cancelOrder(orderId: string): void {
    const order = this.findOrder(orderId);

    if (!order) {
      return;
    }

    this.preOrdersService
      .cancelPreOrderByReservationId(order.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.closeCancelModal();
          this.loadOrders();
        },
        error: (error) => {
          console.error(
            `Error cancelling order ${orderId}:`,
            error,
          );
        },
      });
  }

  editOrder(orderId: string): void {
    const order = this.findOrder(orderId);

    if (!order) {
      return;
    }

    this.preOrdersService
      .unsubmitPreOrderByReservationId(order.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadOrders();
        },
        error: (error) => {
          console.error(
            `Error un-submitting order ${orderId}:`,
            error,
          );
        },
      });
  }

  submitOrder(orderId: string): void {
    const order = this.findOrder(orderId);

    if (!order) {
      return;
    }

    const group = this.groups()[order.reservationId];
    const hasSubmittedGuests = !!group?.participants.some(
      (participant) => participant.isSubmitted && participant.items.length > 0,
    );

    // Submitting a group pre-order merges dishes of the submitted guests into the order
    // and submits it in one step, otherwise a regular pre-order submit is enough.
    const submit$: Observable<unknown> =
      group && hasSubmittedGuests
        ? this.groupPreOrderService.submit$(group.groupReservationId, order.reservationId)
        : this.preOrdersService.submitPreOrderByReservationId(order.reservationId);

    submit$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.submitOrderId.set(orderId);
        this.loadOrders();
      },
      error: (error: HttpErrorResponse) => {
        console.error(`Error submitting order ${orderId}:`, error);
        this.toastService.error(getServerErrorMessage(error));
      },
    });
  }

  groupFor(order: Order): GroupPreOrder | null {
    return this.groups()[order.reservationId] ?? null;
  }

  refreshGroup(orderId: string): void {
    const order = this.findOrder(orderId);

    if (!order) {
      return;
    }

    this.groupPreOrderService
      .getGroupForReservation$(order.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((group) => this.setGroup(order.reservationId, group));
  }

  removeParticipant(orderId: string, guestSessionId: string): void {
    const order = this.findOrder(orderId);
    const group = order ? this.groups()[order.reservationId] : undefined;

    if (!order || !group) {
      return;
    }

    this.groupPreOrderService
      .removeParticipant$(group.groupReservationId, guestSessionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (snapshot) =>
          this.setGroup(order.reservationId, { ...snapshot, groupReservationId: group.groupReservationId }),
        error: (error: HttpErrorResponse) => {
          console.error(`Error removing guest session ${guestSessionId}:`, error);
          this.toastService.error(getServerErrorMessage(error));
        },
      });
  }

  addDishToOrder(orderId: string): void {
    console.log(`Adding a dish to order ${orderId}`);
    void this.router.navigate(['/menu']);
  }

  updateDishQuantity(
    orderId: string,
    itemId: string,
    quantity: number,
  ): void {
    if (quantity <= 0) {
      return;
    }

    const originalOrder = this.findOrder(orderId);

    if (!originalOrder) {
      return;
    }

    const updatedOrder: Order = {
      ...originalOrder,
      orderItems: originalOrder.orderItems.map((item) =>
        item.dish.id === itemId
          ? {
            ...item,
            quantity,
          }
          : item,
      ),
    };

    this.preOrdersService
      .editPreOrderByReservationId(
        updatedOrder.reservationId,
        updatedOrder,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadOrders();
        },
        error: (error) => {
          console.error(
            `Error updating item ${itemId}:`,
            error,
          );
        },
      });
  }

  removeDishFromOrder(
    orderId: string,
    itemId: string,
  ): void {
    const originalOrder = this.findOrder(orderId);

    if (!originalOrder) {
      return;
    }

    const updatedOrder: Order = {
      ...originalOrder,
      orderItems: originalOrder.orderItems.filter(
        (item) => item.dish.id !== itemId,
      ),
    };

    this.preOrdersService
      .editPreOrderByReservationId(
        updatedOrder.reservationId,
        updatedOrder,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadOrders();
        },
        error: (error) => {
          console.error(
            `Error removing item ${itemId}:`,
            error,
          );
        },
      });
  }

  private findOrder(orderId: string): Order | undefined {
    const order = this.orders().find(
      (item) => item.orderId === orderId,
    );

    if (!order) {
      console.error(`Order with ID ${orderId} not found`);
    }

    return order;
  }

  private setGroup(reservationId: string, group: GroupPreOrder | null): void {
    this.groups.update((groups) => {
      const next = { ...groups };

      if (group) {
        next[reservationId] = group;
      } else {
        delete next[reservationId];
      }

      return next;
    });
  }

  private loadGroups(orders: Order[]): void {
    const unsubmitted = orders.filter((order) => order.status === 0);

    if (unsubmitted.length === 0) {
      this.groups.set({});
      return;
    }

    forkJoin(
      unsubmitted.map((order) =>
        this.groupPreOrderService
          .getGroupForReservation$(order.reservationId)
          .pipe(map((group) => [order.reservationId, group] as const)),
      ),
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((entries) => {
        const groups: Record<string, GroupPreOrder> = {};

        for (const [reservationId, group] of entries) {
          if (group) {
            groups[reservationId] = group;
          }
        }

        this.groups.set(groups);
      });
  }

  private loadOrders(): void {
    this.preOrdersService
      .getPreOrders()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (orders) => {
          this.orders.set(orders);
          this.loadGroups(orders);
        },
        error: (error) => {
          console.error('Error loading orders:', error);
          this.orders.set([]);
          this.groups.set({});
        },
      });
  }
}
