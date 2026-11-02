using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using AutoFixture;
using Moq;

namespace APIHandler.Tests.Services;

public class GuestSessionServiceGroupTests
{
    private const string OwnerId = "owner-1";

    private readonly Fixture _fixture = new();
    private readonly Mock<IGuestSessionRepository> _sessionRepository = new();
    private readonly Mock<IGroupReservationRepository> _groupRepository = new();
    private readonly Mock<IDishRepository> _dishRepository = new();
    private readonly Mock<IReservationRepository> _reservationRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ITableRepository> _tableRepository = new();
    private readonly GuestSessionService _service;

    private readonly Reservation _reservation;
    private readonly GroupReservation _group;

    public GuestSessionServiceGroupTests()
    {
        _service = new GuestSessionService(
            _groupRepository.Object,
            _sessionRepository.Object,
            _dishRepository.Object,
            _reservationRepository.Object,
            _userRepository.Object,
            _tableRepository.Object);

        _reservation = _fixture.Build<Reservation>()
            .With(r => r.Id, "reservation-1")
            .With(r => r.CustomerId, OwnerId)
            .Create();

        _group = _fixture.Build<GroupReservation>()
            .With(g => g.GroupReservationId, "group-1")
            .With(g => g.ReservationId, _reservation.Id)
            .Create();

        _reservationRepository.Setup(r => r.GetReservationByIdAsync(_reservation.Id)).ReturnsAsync(_reservation);
        _groupRepository.Setup(r => r.GetGroupReservationByIdAsync(_group.GroupReservationId)).ReturnsAsync(_group);
        _groupRepository.Setup(r => r.GetByReservationIdAsync(_reservation.Id)).ReturnsAsync(_group);
    }

    private Dish Dish(string id, decimal price) =>
        _fixture.Build<Dish>().With(d => d.Id, id).With(d => d.Price, price).Create();

    private GuestSession Guest(string id, string name, bool submitted, params (string DishId, int Quantity)[] dishes) =>
        _fixture.Build<GuestSession>()
            .With(s => s.Id, id)
            .With(s => s.GuestName, name)
            .With(s => s.GroupReservationId, _group.GroupReservationId)
            .With(s => s.IsOwnerSession, false)
            .With(s => s.IsSubmitted, submitted)
            .With(s => s.Dishes, dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity }).ToList())
            .Create();

    private void SetupDishes(params Dish[] dishes) =>
        _dishRepository
            .Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(dishes);

    [Fact]
    public async Task GetGroupSessionsAsync_Owner_ReturnsParticipantsWithTotals()
    {
        var sessions = new List<GuestSession>
        {
            Guest("session1", "Andrii", true, ("dish1", 2)),
            Guest("session2", "Olena", false, ("dish2", 1)),
        };
        _sessionRepository.Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId)).ReturnsAsync(sessions);
        SetupDishes(Dish("dish1", 180m), Dish("dish2", 220m));

        GetAllSessionsResponse result = await _service.GetGroupSessionsAsync(OwnerId, _group.GroupReservationId);

        var participants = result.Participants.ToList();
        Assert.Equal(2, participants.Count);
        Assert.Equal(360m, participants.Single(p => p.Id == "session1").Total);
        Assert.True(participants.Single(p => p.Id == "session1").IsSubmitted);
        Assert.Equal(220m, participants.Single(p => p.Id == "session2").Total);
        Assert.False(participants.Single(p => p.Id == "session2").IsSubmitted);
        Assert.Equal(580m, result.Total);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_NoSessions_ReturnsEmptyList()
    {
        _sessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId))
            .ReturnsAsync(new List<GuestSession>());
        SetupDishes();

        GetAllSessionsResponse result = await _service.GetGroupSessionsAsync(OwnerId, _group.GroupReservationId);

        Assert.Empty(result.Participants);
        Assert.Equal(0m, result.Total);
    }

    [Fact]
    public async Task GetGroupSessionsAsync_NotOwner_ThrowsActionForbidden()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.GetGroupSessionsAsync("someone-else", _group.GroupReservationId));
    }

    [Fact]
    public async Task GetGroupSessionsAsync_GroupNotFound_ThrowsGroupReservationNotFound()
    {
        _groupRepository.Setup(r => r.GetGroupReservationByIdAsync("missing")).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _service.GetGroupSessionsAsync(OwnerId, "missing"));
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_ValidSession_DeletesAndReturnsUpdatedSnapshot()
    {
        GuestSession toDelete = Guest("session1", "Andrii", true, ("dish1", 2));
        GuestSession remaining = Guest("session2", "Olena", false, ("dish2", 1));

        _sessionRepository.Setup(r => r.GetByGuestSessionId("session1")).ReturnsAsync(toDelete);
        _sessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId))
            .ReturnsAsync([remaining]);
        SetupDishes(Dish("dish2", 220m));

        GetAllSessionsResponse result = await _service.DeleteGuestSessionAsync(OwnerId, _group.GroupReservationId, "session1");

        _sessionRepository.Verify(r => r.DeleteAsync(toDelete), Times.Once);
        Assert.Single(result.Participants);
        Assert.Equal("session2", result.Participants.Single().Id);
        Assert.Equal(220m, result.Total);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_SessionNotFound_ThrowsGuestSessionNotFound()
    {
        _sessionRepository.Setup(r => r.GetByGuestSessionId("missing")).ReturnsAsync((GuestSession?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() =>
            _service.DeleteGuestSessionAsync(OwnerId, _group.GroupReservationId, "missing"));
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_SessionFromAnotherGroup_ThrowsValidationException()
    {
        GuestSession foreign = Guest("session9", "Foreign", false);
        foreign.GroupReservationId = "another-group";
        _sessionRepository.Setup(r => r.GetByGuestSessionId("session9")).ReturnsAsync(foreign);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.DeleteGuestSessionAsync(OwnerId, _group.GroupReservationId, "session9"));

        _sessionRepository.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task DeleteGuestSessionAsync_NotOwner_ThrowsActionForbidden()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.DeleteGuestSessionAsync("someone-else", _group.GroupReservationId, "session1"));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NoOwnerSession_CreatesOne()
    {
        _sessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId))
            .ReturnsAsync([Guest("session1", "Andrii", false)]);

        GuestSession? saved = null;
        _sessionRepository
            .Setup(r => r.SaveAsync(It.IsAny<GuestSession>()))
            .Callback<GuestSession>(s => saved = s);

        GetGuestSessionIdResponse result = await _service.GetOwnerGuestSessionIdAsync(OwnerId, _reservation.Id);

        Assert.NotNull(saved);
        Assert.True(saved!.IsOwnerSession);
        Assert.Equal(OwnerId, saved.OwnerUserId);
        Assert.Equal(saved.Id, result.GuestSessionId);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_ExistingOwnerSession_ReturnsItWithoutCreatingNew()
    {
        GuestSession owner = Guest("owner-session", "Owner", false);
        owner.IsOwnerSession = true;
        _sessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId))
            .ReturnsAsync([owner]);

        GetGuestSessionIdResponse result = await _service.GetOwnerGuestSessionIdAsync(OwnerId, _reservation.Id);

        Assert.Equal("owner-session", result.GuestSessionId);
        _sessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NotOwner_ThrowsActionForbidden()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.GetOwnerGuestSessionIdAsync("someone-else", _reservation.Id));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_ReservationNotFound_ThrowsReservationNotFound()
    {
        _reservationRepository.Setup(r => r.GetReservationByIdAsync("missing")).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _service.GetOwnerGuestSessionIdAsync(OwnerId, "missing"));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NoGroup_ThrowsGroupReservationNotFound()
    {
        _groupRepository.Setup(r => r.GetByReservationIdAsync(_reservation.Id)).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _service.GetOwnerGuestSessionIdAsync(OwnerId, _reservation.Id));
    }

    [Fact]
    public async Task GetGroupReservationStatusAsync_OwnerSessionExists_CountsOwnerOnce()
    {
        GuestSession ownerSession = _fixture.Build<GuestSession>()
            .With(s => s.GroupReservationId, _group.GroupReservationId)
            .With(s => s.IsOwnerSession, true)
            .Create();
        var sessions = new List<GuestSession> { ownerSession, Guest("session1", "Andrii", true) };
        _sessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId))
            .ReturnsAsync(sessions);

        GroupReservationStatusResponse result = await _service.GetGroupReservationStatusAsync(_group.GroupReservationId);

        Assert.Equal(2, result.CurrentParticipants);
    }
}
