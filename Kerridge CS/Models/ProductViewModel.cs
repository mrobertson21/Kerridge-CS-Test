using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Kerridge_CS.Models
{
	public class TaxViewModel
	{
		public TaxViewModel()
		{
		}

        public IEnumerable<ProductModel>? Products { get; set; }
        public List<SelectListItem>? ProductList { get; set; }

        [Display(Name = "Product")]
        [StringLength(50)]
        public string? ProductName { get; set; }

        public int ProductQuantity { get; set; }
        public decimal ProductPrice { get; set; }
    }
}

