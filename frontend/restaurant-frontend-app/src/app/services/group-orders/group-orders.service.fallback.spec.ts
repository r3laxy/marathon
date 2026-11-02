import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from './group-orders.service';

const BASE_URL = 'https://api.test';

describe('GroupOrdersService · getGroupInvite$ for a full group', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: BASE_URL }],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('falls back to the reservation when the invite says GuestLimitExceeded', () => {
    let invite: GuestInvite | undefined;
    service.getGroupInvite$('res-1').subscribe((value) => (invite = value));

    http.expectOne(`${BASE_URL}/reservations/res-1/invite-token`).flush({ token: 'tok', groupReservationId: 'grp-1' });
    http
      .expectOne(`${BASE_URL}/guest/invites/tok`)
      .flush({ message: 'Guest limit exceeded', errorCode: 'GuestLimitExceeded' }, { status: 400, statusText: 'Bad Request' });
    http.expectOne(`${BASE_URL}/reservations/res-1`).flush({
      restaurantName: 'Green & Tasty',
      locationAddress: '14 Khreshchatyk St, Kyiv',
      startTime: '2026-11-05T10:30:00Z',
      endTime: '2026-11-05T12:00:00Z',
      guestCount: 2,
    });

    expect(invite?.GroupReservationId).toBe('grp-1');
    expect(invite?.LocationAddress).toBe('14 Khreshchatyk St, Kyiv');
    expect(invite?.MaxGuestCount).toBe(2);
  });

  it('rethrows other invite errors', () => {
    let status = 0;
    service.getGroupInvite$('res-1').subscribe({ error: (error) => (status = error.status) });

    http.expectOne(`${BASE_URL}/reservations/res-1/invite-token`).flush({ token: 'tok', groupReservationId: 'grp-1' });
    http.expectOne(`${BASE_URL}/guest/invites/tok`).flush({}, { status: 404, statusText: 'Not Found' });

    expect(status).toBe(404);
  });
});
