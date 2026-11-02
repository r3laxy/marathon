import { CurrencyPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from '@angular/core';

import { DishImagesService } from '@app/services/dishes/dish-images.service';
import { Order } from '@app/models/orders/order.model';

@Component({
  selector: 'app-submitted-order',
  standalone: true,
  imports: [CurrencyPipe],
  templateUrl: './submitted-order.component.html',
  styleUrl: './submitted-order.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmittedOrderComponent {
  private readonly dishImages = inject(DishImagesService);

  imageFor(dish: { id: string; imageUrl?: string }): string | undefined {
    return dish.imageUrl || this.dishImages.urlFor(dish.id);
  }

  readonly order = input.required<Order>();

  readonly canceled = output<void>();
  readonly edited = output<void>();

  readonly totalPrice = computed(() =>
    this.order()
      .orderItems.filter((item) => item.state !== 2)
      .reduce(
        (total, item) =>
          total + item.dish.price * item.quantity,
        0,
      ),
  );

  cancelOrder(): void {
    this.canceled.emit();
  }

  editOrder(): void {
    this.edited.emit();
  }
}
