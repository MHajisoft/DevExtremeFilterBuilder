using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DevExtremeFilterBuilder.Tests
{
    public class NotExpressionTests
    {

        readonly Products products = new Products();



        [Fact]
        public void Simple_not()
        {

            var jsonFilter = @"
[
    ""!"",
    [""Cost"", ""<"", 500]
]";


            var expectedFilteredList = new List<Product>()
            {
                products.Product5, products.Product6, products.Product7, products.Product8, products.Product9
            };

            FilterBuilder builder = new();
            var expression = builder.GetExpression<Product>(jsonFilter);
            var predicate = expression.Compile();

            var actualFilteredList = products.All.Where(predicate);

            Assert.Equal(expectedFilteredList, actualFilteredList);

        }

    }
}
