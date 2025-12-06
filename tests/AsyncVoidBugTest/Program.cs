using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncVoidBugTest
{
    /// <summary>
    /// Demonstrates the async void bug in event handlers.
    /// 
    /// Bug #31: RoomService.cs has async void Client_LoggedIn
    /// Without try-catch, unhandled exceptions crash the process.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine("ASYNC VOID BUG TEST");
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine();
            Console.WriteLine("This demonstrates how async void event handlers can crash a process");
            Console.WriteLine("when an unhandled exception occurs.");
            Console.WriteLine();

            // Set up unhandled exception handler to detect crashes
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Console.WriteLine($"\u001b[31m✗ PROCESS CRASH: {((Exception)e.ExceptionObject).Message}\u001b[0m");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Console.WriteLine($"\u001b[33m⚠ Unobserved Task Exception: {e.Exception.Message}\u001b[0m");
                e.SetObserved(); // Prevent crash for demo purposes
            };

            // Test 1: Fixed handler (with try-catch) - SLSKDN
            Console.WriteLine("\u001b[33mTest 1: SLSKDN Fixed Handler (with try-catch)\u001b[0m");
            Console.WriteLine("-".PadRight(50, '-'));
            
            var fixedService = new FixedRoomService();
            fixedService.SimulateLogin();
            await Task.Delay(500); // Wait for async operation
            Console.WriteLine("\u001b[32m✓ Process still running after fixed handler\u001b[0m\n");

            // Test 2: Show what WOULD happen with upstream (but don't actually crash)
            Console.WriteLine("\u001b[33mTest 2: UPSTREAM Behavior Explanation\u001b[0m");
            Console.WriteLine("-".PadRight(50, '-'));
            Console.WriteLine("UPSTREAM CODE (vulnerable):");
            Console.WriteLine("  private async void Client_LoggedIn(object sender, EventArgs e)");
            Console.WriteLine("  {");
            Console.WriteLine("      await DoSomethingAsync(); // If throws, PROCESS CRASHES!");
            Console.WriteLine("  }");
            Console.WriteLine();
            Console.WriteLine("SLSKDN FIX:");
            Console.WriteLine("  private async void Client_LoggedIn(object sender, EventArgs e)");
            Console.WriteLine("  {");
            Console.WriteLine("      try");
            Console.WriteLine("      {");
            Console.WriteLine("          await DoSomethingAsync();");
            Console.WriteLine("      }");
            Console.WriteLine("      catch (Exception ex)");
            Console.WriteLine("      {");
            Console.WriteLine("          Log.Error(ex, \"Error in handler\");");
            Console.WriteLine("          // Process continues running");
            Console.WriteLine("      }");
            Console.WriteLine("  }");
            Console.WriteLine();

            // Summary
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine("SUMMARY");
            Console.WriteLine("=".PadRight(70, '='));
            Console.WriteLine();
            Console.WriteLine("async void methods don't propagate exceptions to callers.");
            Console.WriteLine("In event handlers, this means:");
            Console.WriteLine();
            Console.WriteLine("  UPSTREAM: Unhandled exception in Client_LoggedIn → PROCESS CRASH");
            Console.WriteLine("  SLSKDN:   try-catch wraps handler → Exception logged, process continues");
            Console.WriteLine();
            Console.WriteLine("\u001b[32m✓ Bug verification complete\u001b[0m");
        }
    }

    /// <summary>
    /// Simulates the fixed RoomService with try-catch
    /// </summary>
    class FixedRoomService
    {
        public event EventHandler? LoggedIn;

        public FixedRoomService()
        {
            LoggedIn += FixedClientLoggedIn;
        }

        // This is how SLSKDN handles it - with try-catch
        private async void FixedClientLoggedIn(object? sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("  Fixed handler: Starting async operation...");
                await SimulateRoomJoinAsync();
                Console.WriteLine("  Fixed handler: Completed successfully");
            }
            catch (Exception ex)
            {
                // This is the fix - catch and log instead of crashing
                Console.WriteLine($"  Fixed handler: Caught exception safely: {ex.Message}");
            }
        }

        private async Task SimulateRoomJoinAsync()
        {
            await Task.Delay(100);
            // Simulate a failure that would crash upstream
            throw new InvalidOperationException("Simulated room join failure (network error)");
        }

        public void SimulateLogin()
        {
            LoggedIn?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// This is how upstream handles it - WITHOUT try-catch
    /// WARNING: Running this would crash the process!
    /// </summary>
    class VulnerableRoomService
    {
        public event EventHandler? LoggedIn;

        public VulnerableRoomService()
        {
            LoggedIn += VulnerableClientLoggedIn;
        }

        // UPSTREAM CODE - async void without try-catch
        // DO NOT USE - This crashes the process on exception!
        private async void VulnerableClientLoggedIn(object? sender, EventArgs e)
        {
            Console.WriteLine("  Vulnerable handler: Starting async operation...");
            await SimulateRoomJoinAsync();
            // If the above throws, the exception escapes to ThreadPool
            // and terminates the process!
        }

        private async Task SimulateRoomJoinAsync()
        {
            await Task.Delay(100);
            throw new InvalidOperationException("Simulated room join failure");
        }

        public void SimulateLogin()
        {
            LoggedIn?.Invoke(this, EventArgs.Empty);
        }
    }
}

