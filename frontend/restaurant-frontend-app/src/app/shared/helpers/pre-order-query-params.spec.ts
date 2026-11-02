import { buildPreOrderQueryParams } from './pre-order-query-params';

describe('buildPreOrderQueryParams', () => {
  const reservation = {
    id: 'res-1',
    tableId: 'table-uuid',
    locationAddress: '14 Khreshchatyk St',
    startTime: new Date('2026-11-10T18:00:00Z'),
    endTime: new Date('2026-11-10T19:30:00Z'),
  };

  it('enables pre-order mode for the reservation', () => {
    const params = buildPreOrderQueryParams(reservation);

    expect(params['preOrder']).toBe('true');
    expect(params['reservationId']).toBe('res-1');
  });

  it('prefers the explicit table name over the table id', () => {
    const params = buildPreOrderQueryParams(reservation, 'Table 4');

    expect(params['reservationLabel']).toContain('14 Khreshchatyk St');
    expect(params['reservationLabel']).toContain('Table 4');
    expect(params['reservationLabel']).not.toContain('table-uuid');
  });

  it('falls back to the reservation table name and then to the table id', () => {
    expect(buildPreOrderQueryParams({ ...reservation, tableName: 'Table 7' })['reservationLabel']).toContain(
      'Table 7',
    );
    expect(buildPreOrderQueryParams(reservation)['reservationLabel']).toContain('Table table-uuid');
  });
});
