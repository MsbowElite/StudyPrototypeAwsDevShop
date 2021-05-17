using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Newtonsoft.Json;
using Shared;
using Shared.Enums;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Reserver
{
    public class Function
    {
        private AmazonDynamoDBClient AmazonDynamoDBClient { get; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            AmazonDynamoDBClient = new AmazonDynamoDBClient(RegionEndpoint.SAEast1);
        }

        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            if (evnt.Records.Count > 1) throw new InvalidOperationException("Only one message can be handled at a time");
            var message = evnt.Records.FirstOrDefault();
            if (message is null) return;
            await ProcessMessageAsync(message, context);
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"Processed message {message.Body}");

            var order = JsonConvert.DeserializeObject<Order>(message.Body);
            order.OrderStatus = OrderStatus.Reserved;

            foreach (var product in order.Products)
            {
                try
                {
                    await ProductStockUpdate(product.Id, product.Quantity);
                    product.Reserved = true;
                    context.Logger.LogLine($"Product updated in stock {product.Id} - {product.Name}");
                }
                catch (ConditionalCheckFailedException)
                {
                    order.JustificationOfCancellation = $"Product out of stock {product.Id} - {product.Name}";
                    order.Cancelled = true;
                    context.Logger.LogLine($"Error: {order.JustificationOfCancellation}");
                    break;
                }
            }

            if (order.Cancelled)
            {
                foreach (var products in order.Products)
                {
                    if (products.Reserved)
                    {
                        await TakeProductBackToStock(products.Id, products.Quantity);
                        products.Reserved = false;
                        context.Logger.LogLine($"Product returned to the stock {products.Id} - {products.Name}");
                    }
                }

                await AmazonUtil.SendToLine(SNSLine.failed, order);
                await order.SaveAsync();
            }
            else
            {
                await AmazonUtil.SendToLine(SQSLine.reserved, order);
                await order.SaveAsync();
            }
        }

        private async Task ProductStockUpdate(string id, int quantidade)
        {
            UpdateItemRequest request = new()
            {
                TableName = "stock",
                ReturnValues = "NONE",
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue{ S = id } }
                },
                UpdateExpression = "SET Quantity = (Quantity - :quantityOfOrder)",
                ConditionExpression = "Quantity >= :quantityOfOrder",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":quantityOfOrder", new AttributeValue { N = quantidade.ToString() } }
                }
            };

            await AmazonDynamoDBClient.UpdateItemAsync(request);
        }

        private async Task TakeProductBackToStock(string id, int quantidade)
        {
            var request = new UpdateItemRequest
            {
                TableName = "stock",
                ReturnValues = "NONE",
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Id", new AttributeValue{ S = id } }
                },
                UpdateExpression = "SET Quantity = (Quantity + :quantityOfOrder)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":quantityOfOrder", new AttributeValue { N = quantidade.ToString() } }
                }
            };

            await AmazonDynamoDBClient.UpdateItemAsync(request);
        }
    }
}
