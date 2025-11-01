# OrderManagement API - HTTP Tests

This directory contains HTTP test files for the OrderManagement API. These tests can be run directly in your IDE (JetBrains Rider, IntelliJ IDEA, or VS Code with REST Client extension).

## Prerequisites

1. **Start the API**: Make sure the OrderManagement API is running
   ```bash
   cd OrderManagement.Api
   dotnet run
   ```
   The API should be available at `http://localhost:5000` (or check console output for actual port)

2. **IDE Support**:
   - **JetBrains Rider/IntelliJ IDEA**: Built-in HTTP Client (no extension needed)
   - **VS Code**: Install the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension

## Files

- **`orders.http`**: Complete test suite for Orders API
  - Create Order
  - Get Order by ID
  - Get User Orders
  - Process Payment
  - Process Complete Order (create + payment)
  - Error scenarios (404 tests)

## Running Tests

### In JetBrains Rider/IntelliJ IDEA

1. Open any `.http` file
2. You'll see green "play" icons next to each request
3. Click the play icon to run a single request
4. Or click "Run All Requests" to run the entire file

### In VS Code (with REST Client extension)

1. Open any `.http` file
2. You'll see "Send Request" above each `###` separator
3. Click "Send Request" to execute that request

## Test Order

Run the tests in this order for the best experience:

1. **Create Order** - Creates a new order and stores the `orderId` in a variable
2. **Get Order by ID** - Retrieves the created order
3. **Get User Orders** - Gets all orders for a user
4. **Process Payment** - Processes payment for the created order
5. **Process Complete Order** - Creates and processes payment in one request
6. **Error Tests** - Tests 404 and error scenarios

## Variables

The tests use variables that can be customized at the top of each file:

```
@baseUrl = http://localhost:5000
@userId = 1
@orderId = 1
@productId1 = 100
@productId2 = 101
```

### Dynamic Variables

Some variables are set dynamically during test execution:
- `orderId` - Automatically set after creating an order

## Response Tests

Each request includes test assertions that verify:
- HTTP status codes (200, 201, 404, etc.)
- Response structure (required fields)
- Business logic (calculations, status values)

Test results are shown in the IDE's HTTP client output.

## Customizing Tests

### Change Base URL

If your API runs on a different port:
```
@baseUrl = http://localhost:5170
```

### Change Test Data

Modify the product IDs, user IDs, prices, etc. in the request bodies:
```json
{
  "productId": 200,
  "productName": "Custom Product",
  "quantity": 3,
  "price": 49.99
}
```

## Tips

- **Sequential Testing**: Tests are designed to run in order (variables from earlier tests are used in later ones)
- **Modify Variables**: Update the variables at the top of the file to test with different data
- **Debugging**: Check the HTTP client console for detailed request/response info
- **Test Assertions**: Green checkmarks indicate passing tests, red X marks indicate failures

## Example Output

When tests pass, you'll see:
```
 Request executed successfully
 Response has orderId
 Total is calculated correctly
 Status is Pending
```

## Troubleshooting

**API Not Responding**
- Make sure the API is running (`dotnet run` in OrderManagement.Api)
- Check the actual port in the console output
- Update `@baseUrl` if needed

**404 Errors**
- Ensure you've created an order first (Test #1)
- Check that `orderId` variable is set correctly

**Test Failures**
- Check the response body in the HTTP client output
- Verify the API is running the latest code
- Check database state if using persistence
