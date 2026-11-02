import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of, switchMap } from 'rxjs';

import {
  GroupPreOrder,
  GroupSnapshot,
  SubmittedGroupDish,
} from '@app/models/orders/group-pre-order.model';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

@Injectable({
  providedIn: 'root',
})
export class GroupPreOrderService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);
  private readonly guestInviteService = inject(GuestInviteService);

  getParticipants$(groupReservationId: string): Observable<GroupSnapshot> {
    return this.http.get<GroupSnapshot>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`,
    );
  }

  removeParticipant$(groupReservationId: string, guestSessionId: string): Observable<GroupSnapshot> {
    return this.http.delete<GroupSnapshot>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions/${encodeURIComponent(guestSessionId)}`,
    );
  }

  submit$(groupReservationId: string, reservationId: string): Observable<SubmittedGroupDish[]> {
    return this.http.post<SubmittedGroupDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      { reservationId },
    );
  }

  /**
   * Loads the group pre-order (participants and totals) of a reservation.
   * Emits null when the reservation has no open group (single guest, expired or already submitted).
   */
  getGroupForReservation$(reservationId: string): Observable<GroupPreOrder | null> {
    return this.guestInviteService.getInviteToken$(reservationId).pipe(
      switchMap(({ groupReservationId }) =>
        groupReservationId
          ? this.getParticipants$(groupReservationId).pipe(
              map((snapshot): GroupPreOrder => ({ ...snapshot, groupReservationId })),
            )
          : of(null),
      ),
      catchError(() => of(null)),
    );
  }
}
