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
        
        public static void PackageLogs(string sourceDir)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var outputFile = Path.Combine(desktopPath, $"MonitorFile_{timestamp}.pmr");

                // 计算目录哈希
                var hash = CalculateDirectoryHash(sourceDir);
                
                // AES加密哈希值
                var encryptedHash = EncryptHash(hash);
                
                using (var fs = new FileStream(outputFile, FileMode.Create))
                using (var zip = new ZipOutputStream(fs))
                {
                    // 配置AES加密
                    zip.Password = ZipPassword;
                    zip.SetLevel(9);
                    zip.UseZip64 = UseZip64.Off;
                    zip.AESKeySize = 256; // 启用AES-256加密
                    zip.SetComment(encryptedHash);

                    var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
                                     .OrderBy(p => p).ToList();

                    foreach (var file in files)
                    {
                        var entryName = Path.GetRelativePath(sourceDir, file);
                        var entry = new ZipEntry(entryName)
                        {
                            DateTime = DateTime.Now,
                            AESKeySize = 256 // 为每个条目设置AES加密
                        };
                        
                        using (var fileStream = File.OpenRead(file))
                        {
                            zip.PutNextEntry(entry);
                            fileStream.CopyTo(zip);
                            zip.CloseEntry();
                        }
                    }
                }

                // 验证打包完整性
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

        private static string CalculateDirectoryHash(string path)
        {
            using var md5 = MD5.Create();
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                             .OrderBy(p => p).ToList();

            foreach (var file in files)
            {
                // 文件名哈希
                var relativePath = Path.GetRelativePath(path, file);
                var pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower());
                md5.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
                
                // 文件内容哈希
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
            aes.IV = new byte[16]; // 固定IV简化实现
            
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
                using var zip = new ZipFile(fs)
                {
                    Password = ZipPassword
                };
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
