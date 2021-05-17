using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Models;
using System;
using System.Threading.Tasks;

namespace Register.Controllers
{
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        [HttpPost]
        public async Task PostAsync([FromBody] Order order)
        {
            order.Id = Guid.NewGuid().ToString();
            order.CreatedAt = DateTime.UtcNow;

            await order.SaveAsync();

            Console.WriteLine($"Order save succeded: id {order.Id}");
        }
    }
}
