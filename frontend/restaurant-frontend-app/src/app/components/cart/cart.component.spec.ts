import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';

import { GroupPreOrder } from '@app/models/orders/group-pre-order.model';
import { Order } from '@app/models/orders/order.model';
import { GroupPreOrderService } from '@app/services/pre-orders/group-pre-order.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ToastService } from '@app/services/toast/toast.service';

import { CartComponent } from './cart.component';

const GROUP: GroupPreOrder = {
  groupReservationId: 'group-1',
  total: 580,
  participants: [
    {
      id: 'session-1',
      type: 'Guest',
      name: 'Andrii',
      items: [{ dish: { id: 'dish-1', name: 'Caesar Salad', price: 180 }, quantity: 2 }],
      total: 360,
      isSubmitted: true,
    },
    {
      id: 'session-2',
      type: 'Guest',
      name: 'Olena',
      items: [{ dish: { id: 'dish-2', name: 'Tom Yum', price: 220 }, quantity: 1 }],
      total: 220,
      isSubmitted: false,
    },
  ],
};

const ORDER = { orderId: 'order-1', reservationId: 'res-1', status: 0 } as Order;

describe('CartComponent', () => {
  let component: CartComponent;
  let fixture: ComponentFixture<CartComponent>;

  let preOrdersServiceMock: jasmine.SpyObj<PreOrdersService>;
  let groupServiceMock: jasmine.SpyObj<GroupPreOrderService>;
  let routerMock: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    preOrdersServiceMock =
      jasmine.createSpyObj<PreOrdersService>(
        'PreOrdersService',
        [
          'getPreOrders',
          'cancelPreOrderByReservationId',
          'unsubmitPreOrderByReservationId',
          'submitPreOrderByReservationId',
          'editPreOrderByReservationId',
        ],
      );

    routerMock = jasmine.createSpyObj<Router>(
      'Router',
      ['navigate'],
    );

    preOrdersServiceMock.getPreOrders.and.returnValue(of([]));

    groupServiceMock = jasmine.createSpyObj<GroupPreOrderService>('GroupPreOrderService', [
      'getGroupForReservation$',
      'removeParticipant$',
      'submit$',
    ]);
    groupServiceMock.getGroupForReservation$.and.returnValue(of(null));

    await TestBed.configureTestingModule({
      imports: [CartComponent],
      providers: [
        {
          provide: PreOrdersService,
          useValue: preOrdersServiceMock,
        },
        {
          provide: GroupPreOrderService,
          useValue: groupServiceMock,
        },
        {
          provide: ToastService,
          useValue: jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']),
        },
        {
          provide: Router,
          useValue: routerMock,
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CartComponent);
    component = fixture.componentInstance;

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit closeCartEvent when closeCart is called', () => {
    spyOn(component.closeCartEvent, 'emit');

    component.closeCart();

    expect(
      component.closeCartEvent.emit,
    ).toHaveBeenCalled();
  });

  it('should return submitted orders with statuses 1 and 3', () => {
    component.orders.set([
      {
        orderId: 'order-1',
        status: 1,
      },
      {
        orderId: 'order-2',
        status: 2,
      },
      {
        orderId: 'order-3',
        status: 3,
      },
      {
        orderId: 'order-4',
        status: 0,
      },
    ] as Order[]);

    expect(
      component
        .submittedOrders()
        .map((order) => order.status),
    ).toEqual([1, 3]);
  });

  it('should return unsubmitted orders with status 0', () => {
    component.orders.set([
      {
        orderId: 'order-1',
        status: 0,
      },
      {
        orderId: 'order-2',
        status: 1,
      },
      {
        orderId: 'order-3',
        status: 3,
      },
    ] as Order[]);

    expect(
      component
        .unsubmittedOrders()
        .map((order) => order.status),
    ).toEqual([0]);
  });

  it('should navigate to reservations when goToReservations is called', () => {
    component.goToReservations();

    expect(routerMock.navigate).toHaveBeenCalledWith([
      '/reservations',
    ]);
  });

  it('should load the group pre-order of a not submitted order', () => {
    component.orders.set([ORDER]);
    groupServiceMock.getGroupForReservation$.and.returnValue(of(GROUP));

    component.refreshGroup('order-1');

    expect(groupServiceMock.getGroupForReservation$).toHaveBeenCalledWith('res-1');
    expect(component.groupFor(ORDER)).toEqual(GROUP);
  });

  it('should submit the group pre-order when a guest has submitted the cart', () => {
    component.orders.set([ORDER]);
    component.groups.set({ 'res-1': GROUP });
    groupServiceMock.submit$.and.returnValue(of([]));

    component.submitOrder('order-1');

    expect(groupServiceMock.submit$).toHaveBeenCalledWith('group-1', 'res-1');
    expect(preOrdersServiceMock.submitPreOrderByReservationId).not.toHaveBeenCalled();
    expect(component.submitOrderId()).toBe('order-1');
  });

  it('should submit a regular pre-order when no guest has submitted the cart', () => {
    component.orders.set([ORDER]);
    component.groups.set({
      'res-1': {
        ...GROUP,
        participants: GROUP.participants.map((participant) => ({ ...participant, isSubmitted: false })),
      },
    });
    preOrdersServiceMock.submitPreOrderByReservationId.and.returnValue(of({}));

    component.submitOrder('order-1');

    expect(preOrdersServiceMock.submitPreOrderByReservationId).toHaveBeenCalledWith('res-1');
    expect(groupServiceMock.submit$).not.toHaveBeenCalled();
  });

  it('should replace the group with the snapshot returned after removing a participant', () => {
    component.orders.set([ORDER]);
    component.groups.set({ 'res-1': GROUP });
    groupServiceMock.removeParticipant$.and.returnValue(
      of({ participants: [GROUP.participants[1]], total: 220 }),
    );

    component.removeParticipant('order-1', 'session-1');

    expect(groupServiceMock.removeParticipant$).toHaveBeenCalledWith('group-1', 'session-1');
    expect(component.groupFor(ORDER)?.participants.map((participant) => participant.id)).toEqual([
      'session-2',
    ]);
    expect(component.groupFor(ORDER)?.total).toBe(220);
    expect(component.groupFor(ORDER)?.groupReservationId).toBe('group-1');
  });
});
