using MainCore.Infrasturecture;
using MainCore.Infrasturecture.Entities;
using MainCore.Shared.Enums;
using MainCore.UI.ViewModels.Tabs.Villages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MainCore.Test;

public sealed class MoveJobTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=False");
    private readonly DbContextOptions<AppDbContext> _options;

    public MoveJobTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private int[] Seed(int villageId, params int[] positions)
    {
        using var context = new AppDbContext(_options);
        var jobs = positions.Select(p => new Job { VillageId = villageId, Position = p, Type = JobTypeEnums.NormalBuild, Content = "{}" }).ToList();
        context.Jobs.AddRange(jobs);
        context.SaveChanges();
        return [.. jobs.Select(x => x.Id)];
    }

    private (int Id, int Position)[] Order(int villageId)
    {
        using var context = new AppDbContext(_options);
        return [.. context.Jobs.Where(x => x.VillageId == villageId).OrderBy(x => x.Position).Select(x => new ValueTuple<int, int>(x.Id, x.Position))];
    }

    private int Move(int id, MoveEnums move)
    {
        using var context = new AppDbContext(_options);
        return context.MoveJob(new JobId(id), move);
    }

    [Theory]
    [InlineData(MoveEnums.Top, new[] { 3, 0, 1, 2 }, 0)]
    [InlineData(MoveEnums.Up, new[] { 0, 1, 3, 2 }, 2)]
    [InlineData(MoveEnums.Down, new[] { 0, 1, 2, 3 }, 3)]
    [InlineData(MoveEnums.Bottom, new[] { 0, 1, 2, 3 }, 3)]
    public void LastJob_MovesAndPersists(MoveEnums move, int[] expectedOrder, int expectedIndex)
    {
        var ids = Seed(1, 0, 1, 2, 3);

        Move(ids[3], move).ShouldBe(expectedIndex);

        Order(1).Select(x => x.Id).ShouldBe(expectedOrder.Select(i => ids[i]));
        Order(1).Select(x => x.Position).ShouldBe([0, 1, 2, 3]);
    }

    [Fact]
    public void RepeatedUp_KeepsMovingTheSameJob()
    {
        var ids = Seed(1, 0, 1, 2, 3);

        Move(ids[3], MoveEnums.Up);
        Move(ids[3], MoveEnums.Up);

        Order(1).Select(x => x.Id).ShouldBe([ids[0], ids[3], ids[1], ids[2]]);
    }

    [Fact]
    public void FirstJob_Up_StaysOnTop()
    {
        var ids = Seed(1, 0, 1, 2);

        Move(ids[0], MoveEnums.Up).ShouldBe(0);

        Order(1).Select(x => x.Id).ShouldBe(ids);
    }

    [Fact]
    public void GappyPositions_StillMoveAndAreRenumbered()
    {
        var ids = Seed(1, 0, 4, 9);

        Move(ids[2], MoveEnums.Up).ShouldBe(1);

        Order(1).ShouldBe([(ids[0], 0), (ids[2], 1), (ids[1], 2)]);
    }

    [Fact]
    public void OtherVillages_AreUntouched()
    {
        var mine = Seed(1, 0, 1);
        var other = Seed(2, 0, 1);

        Move(mine[1], MoveEnums.Top);

        Order(2).ShouldBe([(other[0], 0), (other[1], 1)]);
    }

    [Fact]
    public void MissingJob_ReturnsMinusOne()
    {
        Move(12345, MoveEnums.Top).ShouldBe(-1);
    }
}
