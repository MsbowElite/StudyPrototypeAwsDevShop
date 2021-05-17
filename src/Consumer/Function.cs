using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Shared;
using Shared.Enums;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Consumer
{
    public class Function
    {
        public async Task FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
        {
            context.Logger.LogLine($"Beginning to process {dynamoEvent.Records.Count} records...");

            foreach (var record in dynamoEvent.Records)
            {
                context.Logger.LogLine($"Event ID: {record.EventID}");
                context.Logger.LogLine($"Event Name: {record.EventName}");

                if (record.EventName == "INSERT")
                {
                    var order = record.Dynamodb.NewImage.ToObject<Order>();
                    order.OrderStatus = OrderStatus.Collected;

                    try
                    {
                        await ProcessValueOfOrder(order);
                        await AmazonUtil.SendToLine(SQSLine.order, order);
                        context.Logger.LogLine($"Collect order succeded: '{order.Id}'");
                    }
                    catch (Exception ex)
                    {
                        context.Logger.LogLine($"Error: '{ex.Message}'");
                        order.JustificationOfCancellation = ex.Message;
                        order.Cancelled = true;
                        await AmazonUtil.SendToLine(SNSLine.failed, order);
                    }

                    await order.SaveAsync();
                }
            }

            context.Logger.LogLine("Stream processing complete.");
        }

        private static async Task ProcessValueOfOrder(Order order)
        {
            foreach (var product in order.Products)
            {
                var produtoDoEstoque = await GetProductDynamoDBAsync(product.Id);
                if (produtoDoEstoque == null) throw new InvalidOperationException($"Product out of stock. {product.Id}");

                product.Value = produtoDoEstoque.ValueTotal;
                product.Name = produtoDoEstoque.Name;
            }

            var valueTotal = order.Products.Sum(x => x.Value * x.Quantity);
            if (order.ValueTotal != 0 && order.ValueTotal != valueTotal)
                throw new InvalidOperationException($"Excpected value of the order is R$ {order.ValueTotal} " +
                    $"and the real value is R$ {valueTotal}");

            order.ValueTotal = valueTotal;
        }

        private static async Task<Order> GetProductDynamoDBAsync(string id)
        {
            var client = new AmazonDynamoDBClient(RegionEndpoint.SAEast1);
            var request = new QueryRequest
            {
                TableName = "stock",
                KeyConditionExpression = "Id = :v_id",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                { { ":v_id", new AttributeValue { S = id } } }
            };

            var response = await client.QueryAsync(request);
            var item = response.Items.FirstOrDefault();
            if (item == null) return null;
            return item.ToObject<Order>();
        }
    }
}
