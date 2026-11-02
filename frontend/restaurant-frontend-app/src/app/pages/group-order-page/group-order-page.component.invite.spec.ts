import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrderPageComponent } from './group-order-page.component';

const dish = { id: 'dish1', name: 'Caesar Salad', price: 180 };
const INVITE = {
  GroupReservationId: 'grp-1',
  LocationAddress: 'Rustaveli Avenue 48',
  MaxGuestCount: 2,
  StartTime: '2026-10-21T16:00:00Z',
  EndTime: '2026-10-21T17:30:00Z',
} as GuestInvite;

function buttonByText(root: ParentNode, text: string): HTMLButtonElement | undefined {
  return [...root.querySelectorAll('button')].find((button) => button.textContent?.trim() === text) as
    | HTMLButtonElement
    | undefined;
}

describe('Group order page · invite more guests', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;

  function create() {
    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        { provide: GroupOrdersService, useValue: service },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success', 'error', 'info']) },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'res-1' }) } } },
      ],
    });
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getGroupInvite$',
      'getSessions$',
      'getOwnerSessionId$',
      'getStatus$',
      'submit$',
      'getInviteToken$',
    ]);
    service.getGroupInvite$.and.returnValue(of(INVITE));
    service.getSessions$.and.returnValue(
      of({
        participants: [
          { id: 'owner', type: 'Owner', name: 'Yevhenii', isSubmitted: false, total: 0, items: [] },
          { id: 'olena', type: 'Guest', name: 'Olena', isSubmitted: true, total: 180, items: [{ dish, quantity: 1 }] },
        ],
        total: 180,
      }),
    );
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getStatus$.and.returnValue(of({ currentParticipants: 3, maxCapacity: 2, status: 'Open' as const }));
    service.getInviteToken$.and.returnValue(of({ token: 'tok-123' }));
  });

  it('counts the displayed participants, not the server counter: «2 of 2 guests»', () => {
    expect(create().element.textContent).toContain('2 of 2 guests');
  });

  it('opens a dialog with the /join link', () => {
    const { fixture, element } = create();
    buttonByText(element, 'Invite more guests')?.click();
    fixture.detectChanges();

    const input = element.querySelector<HTMLInputElement>('[role="dialog"] input');
    expect(input?.value).toContain('/join/tok-123');
    expect(service.getInviteToken$).toHaveBeenCalledOnceWith('res-1');
  });

  it('explains how to add seats when the group is full', () => {
    const { fixture, element } = create();
    buttonByText(element, 'Invite more guests')?.click();
    fixture.detectChanges();

    expect(element.querySelector('[role="dialog"]')?.textContent).toContain('All 2 seats are taken');
  });
});
