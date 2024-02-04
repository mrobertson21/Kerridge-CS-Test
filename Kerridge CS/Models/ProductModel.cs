using System;
namespace Kerridge_CS.Models
{
	public class ProductModel
	{
		public ProductModel()
		{
		}

		public int Id { get; set; }
        public string? Name { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
		public decimal Tax { get; set; }
		public decimal TotalPrice { get; set; } // Price plus tax.
    }
}

