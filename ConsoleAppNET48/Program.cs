using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace ConsoleAppNET48
{
  internal class Program
  {
    static void Main()
    {
      // This is a .NET Framework 4.8 console application.
      // It can be used to test compatibility with .NET Framework 4.8.
      Console.WriteLine("Hello from .NET Framework 4.8 console application");
      Console.WriteLine("This application can only be run once as it applies the singleton pattern");

      bool createdNew;
      bool hasHandle = false;
      string mutexName = @"Global\MonApplicationUnique";

      // Sécurité pour permettre l'accès à tous
      var security = new MutexSecurity();
      var rule = new MutexAccessRule(
          new SecurityIdentifier(WellKnownSidType.WorldSid, null), // "Everyone"
          MutexRights.FullControl,
          AccessControlType.Allow);
      security.AddAccessRule(rule);

      using (var mutex = new Mutex(false, mutexName, out createdNew, security))
      {
        try
        {
          // Tente de prendre le mutex immédiatement
          hasHandle = mutex.WaitOne(0, false);

          if (!hasHandle)
          {
            Console.WriteLine("L'application est déjà en cours d'exécution.");
            return;
          }

          Console.WriteLine("Application démarrée. Appuyez sur Entrée pour quitter...");
          Console.ReadLine();
        }
        finally
        {
          // Libération uniquement si on détient le mutex
          if (hasHandle)
          {
            mutex.ReleaseMutex();
          }
        }
      }

      Console.WriteLine("Press any key to exit...");
      Console.ReadKey();
    }
  }
}
