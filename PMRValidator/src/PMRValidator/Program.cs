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

        static int Main(string[] args)
        {
            Console.WriteLine("PMR 文件验证工具 v1.0");
            Console.WriteLine("=====================\n");
            
            return Parser.Default.ParseArguments<Options>(args).MapResult(
                options => RunValidation(options),
                errors => 
                {
                    Console.WriteLine("参数解析错误：");
                    foreach (var error in errors)
                        Console.WriteLine($"- {error.Tag}");
                    return 1;
                }
            );
        }

        static int RunValidation(Options options)
        {
            try
            {
                Console.WriteLine($"输入文件: {options.InputFile}");
                Console.WriteLine($"解压目录: {options.ExtractPath ?? "[临时目录]"}");
                Console.WriteLine(new string('-', 50));

                // 验证输入文件
                if (!File.Exists(options.InputFile))
                {
                    Console.WriteLine($"✖ 错误：文件 {options.InputFile} 不存在");
                    return 2;
                }

                // 准备解压路径
                var tempExtract = string.IsNullOrEmpty(options.ExtractPath);
                var extractDir = options.ExtractPath ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                string zipFile = Path.ChangeExtension(options.InputFile, ".zip");
                try
                {
                    Console.WriteLine($"步骤1/4: 准备临时文件 {zipFile}");
                    File.Copy(options.InputFile, zipFile, true);

                    Console.WriteLine("步骤2/4: 打开压缩包验证结构");
                    using (var fs = File.OpenRead(zipFile))
                    using (var zip = new ZipFile(fs))
                    {
                        zip.Password = ZipPassword;
                        
                        // 验证文件注释
                        Console.WriteLine("步骤3/4: 验证文件元数据");
                        var encryptedHash = zip.ZipFileComment;
                        if (string.IsNullOrEmpty(encryptedHash))
                        {
                            Console.WriteLine("✖ 错误：文件注释丢失（可能不是有效的PMR文件）");
                            return 3;
                        }

                        // 解压文件
                        Console.WriteLine($"步骤4/4: 解压到 {extractDir}");
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
                            Console.WriteLine("[完成]");
                        }

                        // 计算哈希并验证
                        Console.WriteLine("\n验证文件完整性...");
                        var actualHash = CalculateDirectoryHash(extractDir);
                        var decryptedHash = DecryptHash(encryptedHash);

                        Console.WriteLine($"预期哈希: {decryptedHash}");
                        Console.WriteLine($"实际哈希: {actualHash}");

                        if (actualHash != decryptedHash)
                        {
                            Console.WriteLine("\n✖ 验证失败：文件哈希不匹配（可能被篡改）");
                            return 4;
                        }
                    }

                    Console.WriteLine("\n✔ 验证成功：文件完整且未被篡改");
                    return 0;
                }
                finally
                {
                    Console.WriteLine("\n清理临时文件...");
                    if (File.Exists(zipFile))
                    {
                        File.Delete(zipFile);
                        Console.WriteLine($"已删除临时文件: {zipFile}");
                    }
                    if (tempExtract && Directory.Exists(extractDir))
                    {
                        Directory.Delete(extractDir, true);
                        Console.WriteLine($"已清理临时目录: {extractDir}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n‼ 发生未处理异常:");
                Console.WriteLine($"类型: {ex.GetType().Name}");
                Console.WriteLine($"信息: {ex.Message}");
                Console.WriteLine($"堆栈:\n{ex.StackTrace}");
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
            return BitConverter.ToString(md5.Hash!).Replace("-", "").ToLower(); // 添加null包容符
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
