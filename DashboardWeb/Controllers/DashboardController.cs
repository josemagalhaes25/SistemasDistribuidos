using Microsoft.AspNetCore.Mvc;
using DashboardWeb.Services;

namespace DashboardWeb.Controllers
{
    public class DashboardController : Controller
    {
        private readonly SensorService _sensorService;

        public DashboardController(SensorService sensorService)
        {
            _sensorService = sensorService;
        }

        public IActionResult Index()
        {
            var dados = _sensorService.GetAllReadings();
            return View(dados); // Passa os dados para a view
        }
    }
}