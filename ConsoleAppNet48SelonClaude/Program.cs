using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace ConsoleAppNet48SelonClaude
{
  internal class Program
  {
    private static Mutex _mutex;
    private static string _mutexName;
    private static FileStream _lockFile;

    static int Main()
    {
      // Génère un nom unique basé sur l'assembly
      string appName = Assembly.GetExecutingAssembly().GetName().Name;
      string appPath = Assembly.GetExecutingAssembly().Location;

      // Crée un identifiant unique pour l'application
      _mutexName = $"Global\\{appName}_{appPath.GetHashCode():X8}";

      Console.WriteLine($"Tentative de démarrage de l'application: {appName}");
      Console.WriteLine($"Mutex utilisé: {_mutexName}");

      try
      {
        // Méthode 1: Utilisation d'un Mutex global
        if (TryAcquireGlobalMutex())
        {
          Console.WriteLine("Mutex acquis avec succès.");

          // Méthode 2 (fallback): Utilisation d'un fichier de verrouillage
          if (TryAcquireFileLock())
          {
            Console.WriteLine("Verrou de fichier acquis avec succès.");
            Console.WriteLine("Application démarrée - Une seule instance autorisée.");
            Console.WriteLine("Appuyez sur une touche pour terminer...");

            // Simulation du travail de l'application
            RunApplication();

            return 0;
          }
          else
          {
            Console.WriteLine("Impossible d'acquérir le verrou de fichier.");
            return 1;
          }
        }
        else
        {
          Console.WriteLine("Une autre instance de l'application est déjà en cours d'exécution.");
          Console.WriteLine("Appuyez sur une touche pour quitter...");
          Console.ReadKey();
          return 1;
        }
      }
      catch (Exception exception)
      {
        Console.WriteLine($"Erreur: {exception.Message}");
        return 1;
      }
      finally
      {
        // Nettoyage des ressources
        Cleanup();
      }
    }

    private static bool TryAcquireGlobalMutex()
    {
      try
      {
        // Configuration de la sécurité pour permettre l'accès à tous les utilisateurs
        var security = new MutexSecurity();
        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var rule = new MutexAccessRule(everyone, MutexRights.FullControl, AccessControlType.Allow);
        security.SetAccessRule(rule);

        // Tentative de création du mutex global
        bool createdNew;
        _mutex = new Mutex(false, _mutexName, out createdNew, security);

        // Tentative d'acquisition du mutex avec timeout
        bool acquired = _mutex.WaitOne(TimeSpan.FromSeconds(5), false);

        if (!acquired)
        {
          _mutex.Close();
          _mutex = null;
          return false;
        }

        return true;
      }
      catch (UnauthorizedAccessException)
      {
        Console.WriteLine("Impossible d'accéder au mutex global (droits insuffisants).");
        return false;
      }
      catch (Exception exception)
      {
        Console.WriteLine($"Erreur lors de la création du mutex: {exception.Message}");
        return false;
      }
    }

    private static bool TryAcquireFileLock()
    {
      try
      {
        // Utilise le répertoire temporaire pour le fichier de verrouillage
        string tempPath = Path.GetTempPath();
        string lockFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}_{Environment.MachineName}.lock";
        string lockFilePath = Path.Combine(tempPath, lockFileName);

        Console.WriteLine($"Fichier de verrouillage: {lockFilePath}");

        // Tentative de création/ouverture exclusive du fichier
        _lockFile = new FileStream(lockFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

        // Écrit des informations sur l'instance actuelle
        using (var writer = new StreamWriter(_lockFile, System.Text.Encoding.UTF8, 1024, true))
        {
          writer.WriteLine($"PID: {Process.GetCurrentProcess().Id}");
          writer.WriteLine($"Utilisateur: {Environment.UserName}");
          writer.WriteLine($"Machine: {Environment.MachineName}");
          writer.WriteLine($"Démarré le: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
          writer.Flush();
        }

        return true;
      }
      catch (IOException)
      {
        Console.WriteLine("Le fichier de verrouillage est déjà utilisé par une autre instance.");
        return false;
      }
      catch (UnauthorizedAccessException)
      {
        Console.WriteLine("Impossible de créer le fichier de verrouillage (droits insuffisants).");
        return false;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Erreur lors de la création du fichier de verrouillage: {ex.Message}");
        return false;
      }
    }

    private static void RunApplication()
    {
      Console.WriteLine();
      Console.WriteLine("=== APPLICATION EN COURS D'EXECUTION ===");
      Console.WriteLine($"PID: {Process.GetCurrentProcess().Id}");
      Console.WriteLine($"Utilisateur: {Environment.UserDomainName}\\{Environment.UserName}");
      Console.WriteLine($"Machine: {Environment.MachineName}");
      Console.WriteLine($"Heure de démarrage: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
      Console.WriteLine();

      // Simulation du travail - remplacez par votre logique métier
      int counter = 0;
      while (!Console.KeyAvailable)
      {
        Console.Write($"\rTravail en cours... {++counter} secondes écoulées");
        Thread.Sleep(1000);
      }

      Console.ReadKey(true); // Consomme la touche pressée
      Console.WriteLine();
      Console.WriteLine("Arrêt de l'application...");
    }

    private static void Cleanup()
    {
      try
      {
        // Libération du mutex
        if (_mutex != null)
        {
          _mutex.ReleaseMutex();
          _mutex.Close();
          _mutex.Dispose();
          Console.WriteLine("Mutex libéré.");
        }

        // Fermeture du fichier de verrouillage
        if (_lockFile != null)
        {
          _lockFile.Close();
          _lockFile.Dispose();
          Console.WriteLine("Fichier de verrouillage fermé.");
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Erreur lors du nettoyage: {ex.Message}");
      }
    }

    // Gestionnaire pour intercepter les signaux de fermeture
    private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
    {
      switch (ctrlType)
      {
        case CtrlTypes.CTRL_C_EVENT:
        case CtrlTypes.CTRL_BREAK_EVENT:
        case CtrlTypes.CTRL_CLOSE_EVENT:
        case CtrlTypes.CTRL_LOGOFF_EVENT:
        case CtrlTypes.CTRL_SHUTDOWN_EVENT:
          Console.WriteLine("Signal de fermeture reçu, nettoyage en cours...");
          Cleanup();
          return true;
        default:
          return false;
      }
    }

    // Import de la fonction Win32 pour gérer les signaux de fermeture
    [System.Runtime.InteropServices.DllImport("Kernel32")]
    public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

    public delegate bool HandlerRoutine(CtrlTypes CtrlType);

    public enum CtrlTypes
    {
      CTRL_C_EVENT = 0,
      CTRL_BREAK_EVENT = 1,
      CTRL_CLOSE_EVENT = 2,
      CTRL_LOGOFF_EVENT = 5,
      CTRL_SHUTDOWN_EVENT = 6
    }

    static Program()
    {
      // Enregistrement du gestionnaire de signaux de fermeture
      SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);
    }
  }
}
