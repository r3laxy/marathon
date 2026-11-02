export interface GroupParticipantItem {
  dish: { id: string; name: string; price: number };
  quantity: number;
}

export interface GroupParticipant {
  id: string;
  type: 'Owner' | 'Guest';
  name: string;
  items: GroupParticipantItem[];
  total: number;
  isSubmitted: boolean;
}

/** Response of GET/DELETE /guest/groups/{groupReservationId}/sessions. */
export interface GroupSnapshot {
  participants: GroupParticipant[];
  total: number;
}

export interface GroupPreOrder extends GroupSnapshot {
  groupReservationId: string;
}

/** Response of POST /group-reservations/{groupReservationId}/submit. */
export interface SubmittedGroupDish {
  dishId: string;
  quantity: number;
}
