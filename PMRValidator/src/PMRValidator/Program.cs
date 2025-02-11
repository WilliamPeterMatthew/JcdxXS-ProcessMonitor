using System;
using System.IO;
using System.Linq;
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
            public required string InputFile { get; set; }

            [Option('e', "extract", HelpText = "Extract directory")]
            public string? ExtractPath { get; set; }
        }

        const string ZipPassword = "CPPUAPA";
        const string CryptoKey = "cppuapa";
        private static readonly byte[] FixedIV = 
            new byte[16] { 0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F,0x10 };

        static int Main(string[] args)
        {
            Console.WriteLine("PMR 文件验证工具 v1.0.0");
            Console.WriteLine("==================================\n");
            
            return Parser.Default.ParseArguments<Options>(args).MapResult(
                options => RunValidation(options),
                errors => 
                {
                    Console.WriteLine("参数错误：");
                    errors.ToList().ForEach(e => Console.WriteLine($"- {e.Tag}"));
                    return 1;
                }
            );
        }

        static int RunValidation(Options options)
        {
            try
            {
                Console.WriteLine($"输入文件\t: {options.InputFile}");
                Console.WriteLine($"解压目录\t: {options.ExtractPath ?? "[自动清理临时目录]"}");
                Console.WriteLine(new string('=', 60));

                if (!File.Exists(options.InputFile))
                {
                    Console.WriteLine("✖ 错误：指定的文件不存在");
                    return 2;
                }

                var tempExtract = string.IsNullOrEmpty(options.ExtractPath);
                var extractDir = options.ExtractPath ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                var zipFile = Path.ChangeExtension(options.InputFile, ".zip");

                try
                {
                    Console.WriteLine($"[1/4] 创建临时文件: {zipFile}");
                    File.Copy(options.InputFile, zipFile, true);

                    Console.WriteLine("[2/4] 打开压缩包验证结构");
                    using (var fs = File.OpenRead(zipFile))
                    using (var zip = new ZipFile(fs))
                    {
                        zip.Password = ZipPassword;

                        Console.WriteLine("[3/4] 验证文件元数据");
                        var encryptedHash = zip.ZipFileComment;
                        if (string.IsNullOrEmpty(encryptedHash))
                        {
                            Console.WriteLine("✖ 错误：文件注释丢失（非标准PMR文件）");
                            return 3;
                        }

                        Console.WriteLine($"[4/4] 解压到目录: {extractDir}");
                        Directory.CreateDirectory(extractDir);
                        foreach (ZipEntry entry in zip)
                        {
                            if (string.IsNullOrEmpty(entry.Name)) continue;

                            Console.Write($"解压: {entry.Name.PadRight(40)}");
                            var targetPath = Path.Combine(extractDir, entry.Name);
                            var targetDir = Path.GetDirectoryName(targetPath);
                            
                            if (!string.IsNullOrEmpty(targetDir))
                                Directory.CreateDirectory(targetDir);
                            
                            using (var entryStream = zip.GetInputStream(entry))
                            using (var fsOutput = File.Create(targetPath))
                            {
                                entryStream.CopyTo(fsOutput);
                            }
                            Console.WriteLine("[OK]");
                        }

                        Console.WriteLine("\n验证文件完整性...");
                        var actualHash = CalculateDirectoryHash(extractDir);
                        var decryptedHash = DecryptHash(encryptedHash);

                        Console.WriteLine($"预期哈希值\t: {decryptedHash}");
                        Console.WriteLine($"实际哈希值\t: {actualHash}");

                        if (actualHash != decryptedHash)
                        {
                            Console.WriteLine("\n✖ 验证失败：文件内容已被篡改");
                            return 4;
                        }
                    }

                    Console.WriteLine("\n✔ 验证成功：文件完整且未被修改");
                    return 0;
                }
                finally
                {
                    Console.WriteLine("\n清理临时资源...");
                    SafeDelete(zipFile);
                    if (tempExtract) SafeDelete(extractDir, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n‼ 严重错误：");
                Console.WriteLine($"错误类型\t: {ex.GetType().Name}");
                Console.WriteLine($"错误信息\t: {ex.Message}");
                Console.WriteLine($"堆栈跟踪\t:\n{ex.StackTrace}");
                return 5;
            }
        }

        static string CalculateDirectoryHash(string path)
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
            return BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower();
        }

        static string DecryptHash(string encryptedHash)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey(CryptoKey);
            aes.IV = FixedIV;
            
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

        static void SafeDelete(string path, bool isDirectory = false)
        {
            try
            {
                if (isDirectory && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    Console.WriteLine($"已删除目录: {path}");
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    Console.WriteLine($"已删除文件: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理失败: {path} - {ex.Message}");
            }
        }
    }
}
