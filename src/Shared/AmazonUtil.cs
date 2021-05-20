using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using Shared.Enums;
using Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared
{
    public static class AmazonUtil
    {
        public static async Task SaveAsync(this Order order)
        {
            AmazonDynamoDBClient amazonDynamoDBClient = new(RegionEndpoint.SAEast1);
            DynamoDBContext dynamoDBContext = new(amazonDynamoDBClient);

            await dynamoDBContext.SaveAsync(order);
        }

        public static T ToObject<T>(this Dictionary<string, AttributeValue> dictionary)
        {
            AmazonDynamoDBClient amazonDynamoDBClient = new(RegionEndpoint.SAEast1);
            DynamoDBContext dynamoDBContext = new(amazonDynamoDBClient);

            var document = Document.FromAttributeMap(dictionary);
            return dynamoDBContext.FromDocument<T>(document);
        }

        public static async Task SendToLine(SQSLine line, Order order)
        {
            var orderJson = JsonConvert.SerializeObject(order);
            var client = new AmazonSQSClient(RegionEndpoint.SAEast1);
            var request = new SendMessageRequest
            {
                QueueUrl = $"https://sqs.sa-east-1.amazonaws.com/355552168393/{line}",
                MessageBody = orderJson
            };

            await client.SendMessageAsync(request);
        }

        public static async Task SendToLine(SNSLine line, Order order)
        {
            await Task.CompletedTask;
        }
    }
}
