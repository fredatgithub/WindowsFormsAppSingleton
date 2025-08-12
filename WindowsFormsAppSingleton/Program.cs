using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsAppSingleton
{
  internal static class Program
  {
    private static Mutex mutex = null;

    /// <summary>
    /// Point d'entrée principal de l'application.
    /// </summary>
    [STAThread]
    static void Main()
    {
      const string appName = "My-super-application-F804D218-48EA-40FA-A1C5-9EDB2F773DF4";
      bool createdNew;

      // Crée ou ouvre un Mutex nommé
      mutex = new Mutex(true, appName, out createdNew);

      if (!createdNew)
      {
        string currentUser = GetUserOfExistingInstance();
        // Une autre instance existe déjà
        MessageBox.Show($"L'application est déjà en cours d'exécution par {currentUser}.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new FormMain());

      // Libération du Mutex à la fermeture
      mutex.ReleaseMutex();
    }

    private static string GetUserOfExistingInstance()
    {
      try
      {
        // Nom du processus actuel
        string processName = Process.GetCurrentProcess().ProcessName;

        // Cherche les autres instances du même process
        var existing = Process.GetProcessesByName(processName).FirstOrDefault(p => p.Id != Process.GetCurrentProcess().Id);

        if (existing != null)
        {
          // Récupère le nom de l'utilisateur Windows qui l'exécute
          return GetProcessOwner(existing.Id);
        }
      }
      catch
      {
        // En cas de problème on renvoie "inconnu"
      }

      return "inconnu";
    }

    private static string GetProcessOwner(int processId)
    {
      try
      {
        string query = $"Select * From Win32_Process Where ProcessID = {processId}";
        using (var searcher = new ManagementObjectSearcher(query))
        {
          foreach (ManagementObject obj in searcher.Get())
          {
            object[] argList = new object[] { string.Empty, string.Empty };
            int returnVal = Convert.ToInt32(obj.InvokeMethod("GetOwner", argList));
            if (returnVal == 0)
            {
              return $"{argList[1]}\\{argList[0]}"; // Domaine\Utilisateur
            }
          }
        }
      }
      catch { }
      return "inconnu";
    }
  }
}
