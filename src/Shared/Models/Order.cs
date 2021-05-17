using Amazon.DynamoDBv2.DataModel;
using Newtonsoft.Json.Converters;
using Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Shared.Models
{
    [DynamoDBTable("orders")]
    public class Order
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public decimal ValueTotal { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<Product> Products { get; set; }

        public Client Cliente { get; set; }

        public Payment Payment { get; set; }

        public string JustificationOfCancellation { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public OrderStatus OrderStatus { get; set; }

        public bool Cancelled { get; set; }
    }
}
