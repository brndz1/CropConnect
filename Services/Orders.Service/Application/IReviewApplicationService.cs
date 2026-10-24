namespace Orders.Service.Application;

public interface IReviewApplicationService
{
    Task<ReviewResult> CreateReviewAsync(Caller caller, CreateReviewCommand command, CancellationToken cancellationToken);

    Task<ReviewResult> GetReviewByOrderIdAsync(Caller caller, Guid orderId, CancellationToken cancellationToken);

    Task<PagedResult<ReviewResult>> ListFarmReviewsAsync(ListFarmReviewsQuery query, CancellationToken cancellationToken);
}
