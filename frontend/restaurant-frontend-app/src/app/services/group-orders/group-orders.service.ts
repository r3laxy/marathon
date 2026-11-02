import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, catchError, map, switchMap, throwError } from 'rxjs';

import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { OwnerSessionResponse } from '@app/models/group-orders/owner-session.model';
import { SubmitGroupPreOrderRequest, SubmittedDish } from '@app/models/group-orders/submit-group-pre-order.model';
import { GroupInviteToken, GuestInvite } from '@app/models/reservations/guest-invite.model';
import { ReservationCustomer } from '@app/models/reservations/reservation.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

export interface GroupOrdersRequestOptions {
  /** Тихий запит (фонова синхронізація): помилка не показує toast. */
  silent?: boolean;
}

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /**
   * reservationId → invite-token → invite (з GroupReservationId, адресою, часом).
   * Публічний invite-ендпоінт відмовляє з GuestLimitExceeded, коли група вже заповнена, —
   * для Owner це не помилка, тому дані бронювання беремо з GET /reservations/{id}.
   */
  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.http
      .get<GroupInviteToken>(`${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/invite-token`)
      .pipe(
        switchMap((response) =>
          this.http
            .get<GuestInvite>(`${this.baseUrl}/guest/invites/${encodeURIComponent(response.token)}`, {
              context: new HttpContext().set(SKIP_ERROR_TOAST, true),
            })
            .pipe(
              catchError((error: unknown) =>
                this.isGuestLimitError(error) && response.groupReservationId
                  ? this.inviteFromReservation$(reservationId, response.groupReservationId)
                  : throwError(() => error),
              ),
            ),
        ),
      );
  }

  getSessions$(groupReservationId: string, options?: GroupOrdersRequestOptions): Observable<GroupSessionsResponse> {
    return this.http.get<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`,
      { context: this.contextFor(options) },
    );
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(`${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`)
      .pipe(map((response) => response.guestSessionId));
  }

  getStatus$(groupReservationId: string, options?: GroupOrdersRequestOptions): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/status`,
      { context: this.contextFor(options) },
    );
  }

  submit$(groupReservationId: string, reservationId: string): Observable<SubmittedDish[]> {
    const body: SubmitGroupPreOrderRequest = { reservationId };
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      body,
    );
  }

  removeSession$(groupReservationId: string, guestSessionId: string): Observable<GroupSessionsResponse> {
    return this.http.delete<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions/${encodeURIComponent(guestSessionId)}`,
    );
  }

  private inviteFromReservation$(reservationId: string, groupReservationId: string): Observable<GuestInvite> {
    return this.http
      .get<ReservationCustomer>(`${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}`)
      .pipe(
        map(
          (reservation): GuestInvite => ({
            GroupReservationId: groupReservationId,
            LocationId: '',
            RestaurantName: reservation.restaurantName,
            LocationAddress: reservation.locationAddress,
            StartTime: String(reservation.startTime),
            EndTime: String(reservation.endTime),
            MaxGuestCount: reservation.guestCount,
            TableNumber: 0,
            InviterName: '',
          }),
        ),
      );
  }

  private isGuestLimitError(error: unknown): boolean {
    if (!(error instanceof HttpErrorResponse) || error.status !== 400) return false;
    const body = error.error as { errorCode?: string } | null;
    return body?.errorCode === 'GuestLimitExceeded';
  }

  private contextFor(options?: GroupOrdersRequestOptions): HttpContext {
    return new HttpContext().set(SKIP_ERROR_TOAST, options?.silent ?? false);
  }
}
