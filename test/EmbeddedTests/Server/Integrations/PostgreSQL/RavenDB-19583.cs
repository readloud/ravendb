﻿#if NET8_0
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EmbeddedTests.Server.Integrations.PostgreSQL
{
    public class RavenDB_19583 : PostgreSqlIntegrationTestBase
    {
        private new class Order
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string OrderLines { get; set; }
        }

        [Fact]
        public async Task CaResizeBufferCorrectlyWhenMassiveColumnsAreNextToLightweightColumns()
        {
            string query = $"from Orders";

            using (var store = GetDocumentStore())
            {
                var order = new Order
                {
                    Id = "orders/1",
                    Name = "OrderOne",
                    OrderLines = string.Join(" ", Enumerable.Range(0, 300_000).Select(i => "a"))
                };
                
                using (var session = store.OpenSession())
                {
                    session.Store(order);
                    session.SaveChanges();
                }

                var result = await Act(store, query);
                Assert.Equal(result.Rows[0]["OrderLines"], order.OrderLines);
            }
        }
    }
}
#endif
