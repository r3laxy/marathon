import { Params } from '@angular/router';

export interface PreOrderReservationInfo {
  id: string;
  tableId: string;
  tableName?: string;
  locationAddress: string;
  startTime: Date | string;
  endTime: Date | string;
}

/**
 * Query params that switch the menu page into "pre-order" mode for a reservation
 * (shows the "add to cart" buttons and the notice with the reservation label).
 */
export function buildPreOrderQueryParams(
  reservation: PreOrderReservationInfo,
  tableName?: string,
): Params {
  const startTime = new Date(reservation.startTime);
  const endTime = new Date(reservation.endTime);

  const tableLabel = tableName || reservation.tableName || `Table ${reservation.tableId}`;

  return {
    preOrder: 'true',
    reservationId: reservation.id,
    reservationLabel: [
      reservation.locationAddress,
      tableLabel,
      startTime.toDateString(),
      `${startTime.toLocaleTimeString()} - ${endTime.toLocaleTimeString()}`,
    ].join(', '),
  };
}
