using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Kerridge_CS.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using TaxCalculator;
using static System.Formats.Asn1.AsnWriter;

namespace Kerridge_CS.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ITaxCalc _taxCalculator;

    public HomeController(ILogger<HomeController> logger, IMemoryCache memoryCache, ITaxCalc taxCalculator)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _taxCalculator = taxCalculator;
    }

    public IActionResult Index(bool reset = true)
    {
        ProductViewModel model = new ProductViewModel();

        try {
            // Clear the cache if a reset is requested (we are using this in place of a persistent store for the purposes of this exercise).
            if (reset) {
                _memoryCache.Remove("prods");
            }
            else {
                // Repopulate the basket from cache to allow editing.
                List<ProductModel>? products;
                if (_memoryCache.TryGetValue("prods", out products)) {
                    model.Products = products;
                }
            }
        }
        catch (Exception ex) {
            return RedirectToAction("Error", new { errMsg = ex.Message });
        }

        // Initialise the products selection list.
        model.ProductList = PopulateProducts();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(ProductViewModel model, int prodId = 0) {
        if (ModelState.IsValid) {
            try {
                string key = "prods";
                List<ProductModel>? products;

                // If there is no product id passed in add the product to the list of chosen products.
                if (prodId == 0) {
                    // Create the new product object from the values entered by the user in the page.
                    ProductModel product = CreateProduct(model);

                    // Check the cache for previously selected products.
                    // We would normally persist this data but for this exercise we'll keep it in cache.
                    if (!_memoryCache.TryGetValue(key, out products)) { // Not in cache so no products added yet.
                        products = new List<ProductModel>();
                    }
                    else { // Must be in the cache, recreate from cache.
                        _memoryCache.TryGetValue(key, out products);
                    }

                    // Set the ID as the max product id + 1, or 1 if it is the first product added to the basket (Ids would normally come in the persistent store).
                    if (products != null && products!.Count() > 0) {
                        product.Id = products!.MaxBy(p => p.Id)!.Id + 1;
                    }
                    else {
                        product.Id = 1;
                    }

                    // Add the new product and reset the cache.
                    products!.Add(product);
                    var cacheExpiryOptions = new MemoryCacheEntryOptions {
                        AbsoluteExpiration = DateTime.Now.AddMinutes(20),
                        Priority = CacheItemPriority.High,
                        SlidingExpiration = TimeSpan.FromMinutes(10),
                        Size = 1024,
                    };
                    _memoryCache.Set(key, products, cacheExpiryOptions);
                }
                else {
                    // There is a prod id passed in so remove that product if the cache is still valid.
                    if (_memoryCache.TryGetValue(key, out products)) {
                        var itemToRemove = products!.Single(r => r.Id == prodId);
                        products!.Remove(itemToRemove);
                    }
                }

                // Add the selected products to the page model.
                model.Products = products;
            }
            catch (Exception ex) {
                return RedirectToAction("Error", new { errMsg = ex.Message });
            }
        }

        // Reinitialise the product select list.
        model.ProductList = PopulateProducts();
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(string errMsg = "")
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, ErrorMessage = errMsg });
    }

    /// <summary>
    /// Populates the list of products for selection.
    /// </summary>
    /// <returns>A list of products.</returns>
    private List<SelectListItem> PopulateProducts() {
        // Normally we would get this data from a persisitent store, but we have hard-coded and used the cache for this exercise for simplicity.
            string key = "products";
        List<SelectListItem>? products;

        if (!_memoryCache.TryGetValue(key, out products)) { // Not in cache so create the list from scratch.
            products = new List<SelectListItem>();
            products.Add(new SelectListItem("<Select a product>", ""));
            products.Add(new SelectListItem("Book", "12.49"));
            products.Add(new SelectListItem("Bottle of perfume", "18.99"));
            products.Add(new SelectListItem("Chocolate bar", "0.85"));
            products.Add(new SelectListItem("Imported bottle of perfume (@47.50)", "47.50"));
            products.Add(new SelectListItem("Imported bottle of perfume (@27.99)", "27.99"));
            products.Add(new SelectListItem("Imported box of chocolates (@11.25)", "11.25"));
            products.Add(new SelectListItem("Imported box of chocolates (@10.00)", "10.00"));
            products.Add(new SelectListItem("Music CD", "14.99"));
            products.Add(new SelectListItem("Packet of paracetamol", "9.75"));

            // Set the cache.
            var cacheExpiryOptions = new MemoryCacheEntryOptions {
                AbsoluteExpiration = DateTime.Now.AddMinutes(20),
                Priority = CacheItemPriority.High,
                SlidingExpiration = TimeSpan.FromMinutes(10),
                Size = 1024,
            };
            _memoryCache.Set(key, products, cacheExpiryOptions);
        }
        else { // Must be in the cache, recreate from cache.
            _memoryCache.TryGetValue(key, out products);
            
        }

        return products!;
    }

    /// <summary>
    /// Creates a new product item for the basket.
    /// </summary>
    /// <param name="model">The product view model.</param>
    /// <returns>The new product item.</returns>
    private ProductModel CreateProduct(ProductViewModel model) {
        ProductModel product = new ProductModel();
        product.Name = model.ProductName;
        product.Price = model.ProductPrice;
        product.Quantity = model.ProductQuantity;

        // Get the product's tax to 2 decimal places.
        product.Tax = decimal.Round(product.Price * _taxCalculator.GetTaxRateForProduct(product.Name!) / 100, 2);

        // Round up the tax to the nearest 0.05.
        product.Tax = _taxCalculator.RoundTaxUp(product.Tax, Convert.ToDecimal(0.05));

        // Set the price + tax.
        product.TotalPrice = product.Price + product.Tax;

        return product;
    }

    public IActionResult Receipt() {
        // We would normally persist the data to a database, but for this exercise we will use the cache for simplicity.
        ProductViewModel model = new ProductViewModel();
        List<ProductModel>? products;

        try {
            // Get the basket from cache.
            if (_memoryCache.TryGetValue("prods", out products)) {
                model.Products = products;
                model.TotalTax = products!.Sum(item => item.Tax);
                model.TotalPrice = products!.Sum(item => item.TotalPrice);
            }
        }
        catch (Exception ex) {
            return RedirectToAction("Error", new { errMsg = ex.Message });
        }

        return View(model);
    }
}

