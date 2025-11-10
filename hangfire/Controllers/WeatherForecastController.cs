using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace hangfire.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpPost("createbackgroundJob")]
        // Fire and forget
        public IActionResult Get()
        {
            var jobId = BackgroundJob.Enqueue(() => Console.WriteLine("Background job executed at: {time}", DateTime.Now));
            return Ok($"Background job created {jobId}");
        }

        /*
         
         Schedule Jobs

Examples of various Hangfire job types:

// Fire and forget
backgroundJobs.Enqueue(() => Console.WriteLine("Executed immediately"));

// Delayed job
backgroundJobs.Schedule(() => Console.WriteLine("Executed after delay"), TimeSpan.FromMinutes(5));

// Recurring job
RecurringJob.AddOrUpdate("daily-task", () => Console.WriteLine("Runs every day"), Cron.Daily);

// Continuations
var jobId = backgroundJobs.Enqueue(() => Console.WriteLine("Step 1"));
backgroundJobs.ContinueJobWith(jobId, () => Console.WriteLine("Step 2"));
         
         
         
         */
    }
}
