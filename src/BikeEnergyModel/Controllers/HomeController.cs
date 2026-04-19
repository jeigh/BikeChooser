using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BikeEnergyModel.Models;
using BikeEnergyModel.Services;

namespace BikeEnergyModel.Controllers;

public class HomeController : Controller
{
    private readonly IEnergyCalculator _calculator;

    public HomeController(IEnergyCalculator calculator)
    {
        _calculator = calculator;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new RideResultsViewModel());
    }

    [HttpPost]
    public IActionResult Index(RideResultsViewModel vm)
    {
        if (ModelState.IsValid)
            vm.Results = _calculator.Calculate(vm.Input);
        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
