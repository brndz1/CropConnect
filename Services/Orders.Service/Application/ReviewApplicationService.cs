using Microsoft.EntityFrameworkCore;
using Orders.Service.Data;
using Orders.Service.Models;

namespace Orders.Service.Application;

public sealed class ReviewApplicationService : IReviewApplicationService
{
    private readonly OrdersDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public ReviewApplicationService(OrdersDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<ReviewResult> CreateReviewAsync(
        Caller caller,
        CreateReviewCommand command,
        CancellationToken cancellationToken)
    {
        var customerId = caller.RequireUserId();

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException(ErrorMessages.OrderNotFound);
        }

        if (order.CustomerId != customerId)
        {
            throw new ForbiddenException(ErrorMessages.OnlyCustomerCanReviewOrder);
        }

        if (order.Status != OrderStatus.PickedUp)
        {
            throw new BusinessRuleException(ErrorMessages.OnlyCompletedOrdersCanBeReviewed);
        }

        var exists = await _dbContext.Reviews
            .AnyAsync(r => r.OrderId == command.OrderId, cancellationToken);

        if (exists)
        {
            throw new ConflictException(ErrorMessages.OrderAlreadyReviewed);
        }

        var review = Review.Create(
            command.OrderId,
            customerId,
            order.FarmId,
            command.Rating,
            command.Comment,
            Now);

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(review);
    }

    public async Task<ReviewResult> GetReviewByOrderIdAsync(
        Caller caller,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var review = await _dbContext.Reviews
            .AsNoTracking()
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

        if (review is null)
        {
            throw new NotFoundException(ErrorMessages.ReviewNotFound);
        }

        caller.EnsureCanView(review.Order);
        return ToResult(review);
    }

    public async Task<PagedResult<ReviewResult>> ListFarmReviewsAsync(
        ListFarmReviewsQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<Review> reviews = _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.FarmId == query.FarmId)
            .OrderByDescending(r => r.CreatedAt);

        var paged = await reviews.ToPagedResultAsync(query.Page, cancellationToken);
        var mapped = paged.Items.Select(ToResult).ToList();

        return new PagedResult<ReviewResult>(mapped, paged.TotalCount, paged.Page, paged.PageSize);
    }

    private static ReviewResult ToResult(Review review) => new(
        review.Id,
        review.OrderId,
        review.CustomerId,
        review.FarmId,
        review.Rating,
        review.Comment,
        review.CreatedAt);
}
