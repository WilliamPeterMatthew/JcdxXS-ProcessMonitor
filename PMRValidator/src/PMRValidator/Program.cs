using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CommandLine;
using ICSharpCode.SharpZipLib.Zip;

namespace PMRValidator
{
    class Program
    {
        public class Options
        {
            [Value(0, Required = true, HelpText = "Input .pmr file path")]
            public string InputFile { get; set; }

            [Option('e', "extract", HelpText = "Extract directory")]
            public string ExtractPath { get; set; }
        }

        const string ZipPassword = "CPPUAPA";
        const string CryptoKey = "cppuapa";

        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<Options>(args).MapResult(
                options => RunValidation(options),
                _ => 1
            );
        }

        static int RunValidation(Options options)
        {
            try
            {
                // 验证输入文件
                if (!File.Exists(options.InputFile))
                {
                    Console.WriteLine($"错误：文件 {options.InputFile} 不存在");
                    return 2;
                }

                // 准备解压路径
                var tempExtract = string.IsNullOrEmpty(options.ExtractPath);
                var extractDir = options.ExtractPath ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                try
                {
                    // 重命名文件并解压
                    var zipFile = Path.ChangeExtension(options.InputFile, ".zip");
                    File.Copy(options.InputFile, zipFile, true);

                    // 解压文件
                    using (var fs = File.OpenRead(zipFile))
                    using (var zip = new ZipFile(fs))
                    {
                        zip.Password = ZipPassword;
                        
                        // 获取加密哈希
                        var encryptedHash = zip.ZipFileComment;
                        if (string.IsNullOrEmpty(encryptedHash))
                        {
                            Console.WriteLine("错误：文件注释丢失");
                            return 3;
                        }

                        // 解压文件
                        Directory.CreateDirectory(extractDir);
                        foreach (ZipEntry entry in zip)
                        {
                            var targetPath = Path.Combine(extractDir, entry.Name);
                            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                            
                            using (var entryStream = zip.GetInputStream(entry))
                            using (var fsOutput = File.Create(targetPath))
                            {
                                entryStream.CopyTo(fsOutput);
                            }
                        }

                        // 计算哈希并验证
                        var actualHash = CalculateDirectoryHash(extractDir);
                        var decryptedHash = DecryptHash(encryptedHash);

                        Console.WriteLine($"预期哈希: {decryptedHash}");
                        Console.WriteLine($"实际哈希: {actualHash}");

                        if (actualHash != decryptedHash)
                        {
                            Console.WriteLine("验证失败：文件已被篡改");
                            return 4;
                        }
                    }

                    Console.WriteLine("验证成功：文件完整");
                    return 0;
                }
                finally
                {
                    // 清理临时文件
                    File.Delete(zipFile);
                    if (tempExtract)
                    {
                        Directory.Delete(extractDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"验证错误: {ex.Message}");
                return 5;
            }
        }

        static string CalculateDirectoryHash(string path)
        {
            using var md5 = MD5.Create();
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
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

        static string DecryptHash(string encryptedHash)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey(CryptoKey);
            aes.IV = new byte[16];
            
            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(encryptedHash);
            var decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(decrypted);
        }

        static byte[] DeriveKey(string password)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
    }
}
