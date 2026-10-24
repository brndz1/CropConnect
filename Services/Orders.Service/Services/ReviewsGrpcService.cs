using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Orders.Service.Application;
using Orders.Service.Protos;

namespace Orders.Service.Services;

public sealed class ReviewsGrpcService : ReviewsService.ReviewsServiceBase
{
    private readonly IReviewApplicationService _reviewService;

    public ReviewsGrpcService(IReviewApplicationService reviewService)
    {
        _reviewService = reviewService;
    }

    [Authorize]
    public override async Task<ReviewInfo> CreateReview(CreateReviewRequest request, ServerCallContext context)
    {
        var caller = context.GetCaller();
        var orderId = RequestParsing.ParseId(request.OrderId, nameof(request.OrderId));

        var command = new CreateReviewCommand(orderId, request.Rating, request.Comment);
        var result = await _reviewService.CreateReviewAsync(caller, command, context.CancellationToken);

        return result.ToProto();
    }

    public override async Task<ReviewInfo> GetReviewByOrder(GetReviewByOrderRequest request, ServerCallContext context)
    {
        var caller = context.GetCaller();
        var orderId = RequestParsing.ParseId(request.OrderId, nameof(request.OrderId));

        var result = await _reviewService.GetReviewByOrderIdAsync(caller, orderId, context.CancellationToken);
        return result.ToProto();
    }

    public override async Task<ListFarmReviewsResponse> ListFarmReviews(ListFarmReviewsRequest request, ServerCallContext context)
    {
        var farmId = RequestParsing.ParseId(request.FarmId, nameof(request.FarmId));
        var page = PageRequest.Create(request.Page, request.PageSize);

        var query = new ListFarmReviewsQuery(farmId, page);
        var result = await _reviewService.ListFarmReviewsAsync(query, context.CancellationToken);

        var response = new ListFarmReviewsResponse
        {
            TotalCount = result.TotalCount
        };

        foreach (var review in result.Items)
        {
            response.Reviews.Add(review.ToProto());
        }

        return response;
    }
}
