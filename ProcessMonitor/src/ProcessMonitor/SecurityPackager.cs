using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;

namespace ProcessMonitor
{
    public static class SecurityPackager
    {
        const string CryptoKey = "cppuapa";
        const string ZipPassword = "CPPUAPA";
        private static readonly byte[] FixedIV = 
            new byte[16] { 0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F,0x10 };
        
        public static void PackageLogs(string sourceDir)
        {
            try
            {
                CleanTempFiles(sourceDir);
                
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var outputFile = Path.Combine(desktopPath, $"MonitorFile_{timestamp}.pmr");

                var hash = CalculateDirectoryHash(sourceDir);
                var encryptedHash = EncryptHash(hash);
                
                using (var fs = new FileStream(outputFile, FileMode.Create))
                using (var zip = new ZipOutputStream(fs))
                {
                    zip.Password = ZipPassword;
                    zip.SetLevel(9);
                    zip.UseZip64 = UseZip64.Off;

                    var files = Directory.GetFiles(sourceDir, "ProcessLog_*.csv")
                                     .OrderBy(p => p).ToList();

                    foreach (var file in files)
                    {
                        var entry = new ZipEntry(Path.GetFileName(file))
                        {
                            DateTime = DateTime.Now,
                            AESKeySize = 256
                        };
                        
                        using (var fileStream = File.OpenRead(file))
                        {
                            zip.PutNextEntry(entry);
                            fileStream.CopyTo(zip);
                            zip.CloseEntry();
                        }
                    }
                    
                    zip.SetComment(encryptedHash);
                }

                if (!ValidatePackage(outputFile, encryptedHash))
                {
                    File.Delete(outputFile);
                    throw new Exception("Package validation failed");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(sourceDir, "packager.log"), 
                    $"[{DateTime.Now}] Packaging failed: {ex}\n");
            }
        }

        private static void CleanTempFiles(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*.tmp"))
            {
                try { File.Delete(file); } catch { }
            }
        }

        private static string CalculateDirectoryHash(string path)
        {
            using var md5 = MD5.Create();
            var files = Directory.GetFiles(path, "ProcessLog_*.csv")
                             .OrderBy(p => p).ToList();

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(path, file);
                var pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower());
                md5.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
                
                using var stream = File.OpenRead(file);
                var contentHash = md5.ComputeHash(stream);
                md5.TransformBlock(contentHash, 0, contentHash.Length, null, 0);
            }
            
            md5.TransformFinalBlock(new byte[0], 0, 0);
            return BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();
        }

        private static string EncryptHash(string hash)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey(CryptoKey);
            aes.IV = FixedIV;
            
            using var encryptor = aes.CreateEncryptor();
            var hashBytes = Encoding.UTF8.GetBytes(hash);
            var encrypted = encryptor.TransformFinalBlock(hashBytes, 0, hashBytes.Length);
            return Convert.ToBase64String(encrypted);
        }

        private static bool ValidatePackage(string zipPath, string expectedHash)
        {
            try
            {
                using var fs = File.OpenRead(zipPath);
                using var zip = new ZipFile(fs);
                zip.Password = ZipPassword;
                return zip.ZipFileComment == expectedHash;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] DeriveKey(string password)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }
}
