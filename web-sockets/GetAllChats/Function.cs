using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using GetAllChats.Models;
using System.Net;
using System.Text.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetAllChats;

public class Function
{
    private readonly AmazonDynamoDBClient _client;
    private readonly DynamoDBContext _context;

    public Function()
    {
        _client = new AmazonDynamoDBClient();
        _context = new DynamoDBContext(_client);
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var userId = request.QueryStringParameters["userId"];

        // Отримати параметри пагінації (якщо вони доступні в запиті)
        int? pageSize = Convert.ToInt32(request.QueryStringParameters["pageSize"]);
        int? pageNumber = Convert.ToInt32(request.QueryStringParameters["pageNumber"]);

        List<Chat> chats = await GetAllChats(userId, pageSize, pageNumber);

        var result = new List<GetAllChatsResponseItem>(chats.Count);

        return new APIGatewayProxyResponse
        {
            StatusCode = (int)HttpStatusCode.OK,
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Access-Control-Allow-Origin", "*" }
            },

            Body = JsonSerializer.Serialize(result)
        };
    }

    private async Task<List<Chat>> GetAllChats(string userId, int? pageSize, int? pageNumber)
    {
        var user1 = new QueryOperationConfig()
        {
            IndexName = "user1-updatedDt-index",
            KeyExpression = new Expression()
            {
                ExpressionStatement = "user1 = :user",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>() { { ":user", userId } }
            }
        };

        var user2 = new QueryOperationConfig()
        {
            IndexName = "user2-updatedDt-index",
            KeyExpression = new Expression()
            {
                ExpressionStatement = "user2 = :user",
                ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>() { { ":user", userId } }
            }
        };

        var user1Results = await _context.FromQueryAsync<Chat>(user1).GetRemainingAsync();
        var user2Results = await _context.FromQueryAsync<Chat>(user2).GetRemainingAsync();

        var allChats = user1Results.Concat(user2Results).OrderBy(x => x.UpdateDt).ToList();

        if (pageSize.HasValue && pageNumber.HasValue)
        {
            allChats = allChats.Skip((pageNumber.Value - 1) * pageSize.Value).Take(pageSize.Value).ToList();
        }

        return allChats;
    }
}