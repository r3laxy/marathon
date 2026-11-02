import {Component, ChangeDetectionStrategy, input, output, computed} from '@angular/core';
import { CommonModule } from '@angular/common';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { DishState } from '@app/models/enums/dish-state.model';
import { DishStateLabels } from '@app/shared/mappers/dish-enum.mapper';
import {CustomButtonComponent} from '@app/shared/custom-components/custom-button/custom-button.component';

@Component({
  selector: 'app-dish-card',
  templateUrl: './dish-card.component.html',
  styleUrls: ['./dish-card.component.scss'],
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [CommonModule, CustomButtonComponent],
})
export class DishCardComponent {
  readonly dish = input.required<DishListResponse>();
  readonly maxWidth = input('');
  readonly preOrderEnabled = input(false);

  readonly dishClicked = output<void>();
  readonly preOrderClicked = output<void>();

  // The API sends labels like "Out of stock" / "On stop"; normalise them to the enum values.
  private readonly normalizedState = computed(() => {
    const key = (this.dish().state ?? '').replace(/[\s_-]/g, '').toLowerCase();

    return (
      Object.values(DishState).find((s) => s.toLowerCase() === key) ?? null
    );
  });

  readonly dishStateLabel = computed(() => {
    const state = this.normalizedState();

    return state ? DishStateLabels[state] : this.dish().state;
  });

  readonly isUnavailable = computed(() => {
    const state = this.normalizedState();

    return state === DishState.OnStop || state === DishState.OutOfStock;
  });

  onCardClicked(): void {
    if (!this.isUnavailable()) {
      this.dishClicked.emit();
    }
  }
}
