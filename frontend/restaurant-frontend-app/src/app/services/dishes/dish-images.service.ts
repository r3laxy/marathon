import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';

import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

/** Photos of every dish by id, for places where the API returns a dish without its image (group carts, submitted orders). */
@Injectable({ providedIn: 'root' })
export class DishImagesService {
  private readonly http = inject(HttpClient, { optional: true });
  private readonly baseUrl = inject(API_BASE_URL, { optional: true });
  private readonly images = signal<Record<string, string>>({});

  constructor() {
    if (!this.http || !this.baseUrl) return;

    this.http
      .get<DishListResponse[]>(`${this.baseUrl}/dishes`, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
      .subscribe({
        next: (dishes) => {
          const map: Record<string, string> = {};
          for (const dish of dishes ?? []) {
            if (dish.imageUrl) map[dish.id] = dish.imageUrl;
          }
          this.images.set(map);
        },
        error: () => undefined,
      });
  }

  urlFor(dishId: string): string | undefined {
    return this.images()[dishId];
  }
}
