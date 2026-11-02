import { DishImagesService } from '@app/services/dishes/dish-images.service';
import { CurrencyPipe, NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';

import { GroupParticipant, GroupPreOrder } from '@app/models/orders/group-pre-order.model';
import { Order } from '@app/models/orders/order.model';

@Component({
  selector: 'app-not-submitted-order',
  standalone: true,
  imports: [CurrencyPipe, NgTemplateOutlet],
  templateUrl: './not-submitted-order.component.html',
  styleUrl: './not-submitted-order.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotSubmittedOrderComponent {
  private readonly dishImages = inject(DishImagesService);
  readonly order = input.required<Order>();
  readonly group = input<GroupPreOrder | null>(null);

  readonly submitted = output<string>();
  readonly addDish = output<void>();
  readonly removeParticipant = output<string>();
  readonly refreshGroup = output<void>();
  readonly removeDish = output<string>();

  /** Dishes whose photo failed to load — a letter avatar is shown instead of broken alt text. */
  readonly brokenImages = signal<ReadonlySet<string>>(new Set());
  readonly updateQuantity = output<{
    itemId: string;
    quantity: number;
  }>();

  readonly totalPrice = computed(() =>
    this.order().orderItems.reduce(
      (total, item) => total + item.dish.price * item.quantity,
      0,
    ),
  );

  /**
   * The owner's own dishes live in this order (the one shown above), not in the owner session,
   * so the owner row of the group block is built from the order itself.
   */
  readonly participants = computed<GroupParticipant[]>(() => {
    const items = this.order().orderItems.map((item) => ({
      dish: { id: item.dish.id, name: item.dish.name, price: item.dish.price },
      quantity: item.quantity,
    }));

    return (this.group()?.participants ?? []).map((participant) =>
      participant.type === 'Owner'
        ? { ...participant, items, total: this.totalPrice(), isSubmitted: participant.isSubmitted || items.length > 0 }
        : participant,
    );
  });

  /** Total of the guests whose carts are submitted — only these dishes are added on group submit. */
  readonly submittedGuestsTotal = computed(() =>
    this.participants()
      .filter((participant) => participant.type !== 'Owner' && participant.isSubmitted)
      .reduce((total, participant) => total + participant.total, 0),
  );

  /** Photo of a guest's dish: from the group data, otherwise from the same dish in the owner's order. */
  guestImageUrl(dish: string | { id: string; imageUrl?: string }): string | undefined {
    const id = typeof dish === 'string' ? dish : dish.id;
    const own = typeof dish === 'string' ? undefined : dish.imageUrl;

    return (
      own ||
      this.order().orderItems.find((item) => item.dish.id === id)?.dish.imageUrl ||
      this.dishImages.urlFor(id) ||
      undefined
    );
  }

  markImageBroken(dishId: string): void {
    this.brokenImages.update((ids) => new Set(ids).add(dishId));
  }

  incrementItemQuantity(itemId: string): void {
    const item = this.findOrderItem(itemId);

    if (item) {
      this.updateItemQuantity(itemId, item.quantity + 1);
    }
  }

  decrementItemQuantity(itemId: string): void {
    const item = this.findOrderItem(itemId);

    if (item && item.quantity > 1) {
      this.updateItemQuantity(itemId, item.quantity - 1);
    }
  }

  submit(): void {
    this.submitted.emit(this.order().orderId);
  }

  addDishToOrder(): void {
    this.addDish.emit();
  }

  removeDishFromOrder(itemId: string): void {
    this.removeDish.emit(itemId);
  }

  private findOrderItem(itemId: string) {
    return this.order().orderItems.find(
      (item) => item.dish.id === itemId,
    );
  }

  private updateItemQuantity(itemId: string, quantity: number): void {
    this.updateQuantity.emit({ itemId, quantity });
  }
}
