using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.DTOs;
using OrderManagement.Api.Models;
using OrderManagement.Business.UseCases.Orders;

namespace OrderManagement.Api.Controllers;

/// <summary>
///     Orders controller - Handles HTTP concerns only
///     - Extract parameters from HTTP
///     - Validate authentication (not shown in this example)
///     - Map DTO to Request
///     - Map Result to DTO
///     - Return HTTP status codes
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    /// <summary>
    ///     Create a new order
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateOrderResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateOrderResponseDto>> CreateOrder(
        [FromBody] CreateOrderDto dto,
        [FromServices] ICreateOrderUseCase useCase)
    {
        // Execute use case - all business logic happens here
        var result = await useCase.Execute(dto.ToRequest());

        // Controller responsibility: Map Result → DTO and return HTTP response
        return CreatedAtAction(nameof(GetOrder), OrderRouteValues.FromOrderId(result.OrderId),
            CreateOrderResponseDto.FromResult(result));
    }

    /// <summary>
    ///     Get order by ID
    /// </summary>
    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(
        [FromRoute] long orderId,
        [FromServices] IGetOrderUseCase useCase)
    {
        try
        {
            // Execute use case - all business logic happens here
            var order = await useCase.Execute(orderId);

            // Map entity to DTO
            return Ok(OrderResponseDto.FromEntity(order));
        }
        catch (Exception ex) when (ex.Message.Contains("not found"))
        {
            // HTTP concern: Convert exceptions to HTTP status codes
            return NotFound(ErrorResponseDto.FromMessage(ex.Message));
        }
    }

    /// <summary>
    ///     Process payment for an order
    /// </summary>
    [HttpPost("{orderId}/payment")]
    [ProducesResponseType(typeof(ProcessPaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProcessPaymentResponseDto>> ProcessPayment(
        [FromRoute] long orderId,
        [FromBody] ProcessPaymentDto dto,
        [FromServices] IProcessPaymentUseCase useCase)
    {
        try
        {
            // Execute use case
            var result = await useCase.Execute(dto.ToRequest(orderId));

            // Return appropriate HTTP response
            if (!result.Success)
            {
                return BadRequest(ErrorResponseDto.FromMessage(result.Message));
            }

            return Ok(ProcessPaymentResponseDto.FromResult(result));
        }
        catch (Exception ex) when (ex.Message.Contains("not found"))
        {
            // HTTP concern: Convert exceptions to HTTP status codes
            return NotFound(ErrorResponseDto.FromMessage(ex.Message));
        }
    }

    /// <summary>
    ///     Process a complete order (create + payment) in one request
    ///     Demonstrates use case chaining
    /// </summary>
    [HttpPost("process")]
    [ProducesResponseType(typeof(ProcessCompleteOrderResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProcessCompleteOrderResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProcessCompleteOrderResponseDto>> ProcessCompleteOrder(
        [FromBody] ProcessCompleteOrderDto dto,
        [FromServices] IProcessCompleteOrderUseCase useCase)
    {
        // Execute use case
        var result = await useCase.Execute(dto.ToRequest());

        // Return HTTP response
        if (!result.PaymentSuccess)
        {
            return BadRequest(ProcessCompleteOrderResponseDto.FromResult(result));
        }

        return CreatedAtAction(nameof(GetOrder), OrderRouteValues.FromOrderId(result.OrderId),
            ProcessCompleteOrderResponseDto.FromResult(result));
    }

    /// <summary>
    ///     Get all orders for a user
    /// </summary>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(OrdersResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrdersResponseDto>> GetUserOrders(
        [FromRoute] long userId,
        [FromServices] IGetUserOrdersUseCase useCase)
    {
        // Execute use case - all business logic happens here
        var orders = await useCase.Execute(userId);

        // Map entities to DTO wrapper
        return Ok(OrdersResponseDto.FromEntities(orders));
    }
}