# validate.py
import argparse
import os
import sys
import hashlib
import tempfile
import shutil
import pyzipper
from Crypto.Cipher import AES
from Crypto.Hash import SHA256
import base64

# 增强的颜色输出支持
class Color:
    RED = '\033[91m'
    GREEN = '\033[92m'
    YELLOW = '\033[93m'
    BLUE = '\033[94m'
    MAGENTA = '\033[95m'
    CYAN = '\033[96m'
    WHITE = '\033[97m'
    RESET = '\033[0m'

def print_color(text, color=Color.WHITE, flush=True):
    """跨平台颜色输出支持"""
    if sys.platform.startswith('win'):
        try:
            from ctypes import windll
            windll.kernel32.SetConsoleMode(windll.kernel32.GetStdHandle(-11), 7)
        except:
            pass
    
    print(f"{color}{text}{Color.RESET}")
    if flush:
        sys.stdout.flush()

def main():
    # 增强的参数解析
    parser = argparse.ArgumentParser(
        description=f'{Color.CYAN}PMR文件完整性验证工具{Color.RESET}',
        add_help=False,
        formatter_class=argparse.RawTextHelpFormatter
    )
    parser.add_argument(
        'pmr_file', 
        nargs='?',
        help='要验证的PMR文件路径'
    )
    parser.add_argument(
        '-e', '--extract-dir',
        help='指定解压目录（可选）'
    )
    
    # 手动处理帮助信息
    if len(sys.argv) == 1 or '-h' in sys.argv or '--help' in sys.argv:
        help_text = f"""
{Color.YELLOW}使用方法：{Color.RESET}
  validate.py [pmr_file] [-e EXTRACT_DIR]

{Color.YELLOW}示例：{Color.RESET}
  validate.py Moni_111_222.pmr
  validate.py Moni_111_222.pmr -e ./extracted
"""
        print_color(help_text, Color.CYAN)
        sys.exit(0)

    try:
        args = parser.parse_args()
        if not args.pmr_file:
            raise ValueError("必须指定PMR文件路径")
    except Exception as e:
        print_color(f"❌ 参数错误: {str(e)}", Color.RED)
        sys.exit(1)

    # 验证流程
    try:
        # 阶段1：读取PMR文件
        print_color("[1/4] 正在读取PMR文件...", Color.BLUE)
        try:
            with pyzipper.AESZipFile(args.pmr_file, 'r') as zf:
                zf.setpassword(b'CPPUAPA')
                stored_hash = zf.comment.decode('utf-8')
                print_color(f"  找到存储的加密哈希: {stored_hash[:16]}...", Color.CYAN)
        except Exception as e:
            print_color(f"❌ 文件读取失败: {str(e)}", Color.RED)
            sys.exit(2)

        # 阶段2：解压文件
        temp_dir = None
        extract_to = args.extract_dir
        if not extract_to:
            temp_dir = tempfile.mkdtemp()
            extract_to = temp_dir
            print_color(f"[2/4] 创建临时目录: {extract_to}", Color.BLUE)
        else:
            os.makedirs(extract_to, exist_ok=True)
            print_color(f"[2/4] 使用指定目录: {extract_to}", Color.BLUE)

        try:
            with pyzipper.AESZipFile(args.pmr_file, 'r') as zf:
                zf.setpassword(b'CPPUAPA')
                zf.extractall(path=extract_to)
                print_color(f"  解压完成，共 {len(zf.infolist())} 个文件", Color.CYAN)
        except Exception as e:
            print_color(f"❌ 解压失败: {str(e)}", Color.RED)
            if temp_dir:
                shutil.rmtree(temp_dir)
            sys.exit(3)

        # 阶段3：计算哈希
        print_color("[3/4] 正在计算目录哈希...", Color.BLUE)
        try:
            current_hash = calculate_directory_hash(extract_to)
            print_color(f"  计算得到原始哈希: {current_hash[:16]}...", Color.CYAN)
        except Exception as e:
            print_color(f"❌ 哈希计算失败: {str(e)}", Color.RED)
            if temp_dir:
                shutil.rmtree(temp_dir)
            sys.exit(4)

        # 阶段4：验证哈希
        print_color("[4/4] 正在验证完整性...", Color.BLUE)
        try:
            encrypted_current = encrypt_hash(current_hash)
            print_color(f"  加密后的计算哈希: {encrypted_current[:16]}...", Color.CYAN)
        except Exception as e:
            print_color(f"❌ 哈希加密失败: {str(e)}", Color.RED)
            if temp_dir:
                shutil.rmtree(temp_dir)
            sys.exit(5)

        # 结果验证
        if encrypted_current == stored_hash:
            print_color("✅ 验证成功：文件完整未被篡改", Color.GREEN)
        else:
            print_color("❌ 验证失败：哈希值不匹配", Color.RED)
            print_color(f"  存储值: {stored_hash}", Color.YELLOW)
            print_color(f"  计算值: {encrypted_current}", Color.YELLOW)

    finally:
        # 清理临时目录
        if temp_dir and os.path.exists(temp_dir):
            print_color(f"清理临时目录: {temp_dir}", Color.CYAN)
            shutil.rmtree(temp_dir)

def calculate_directory_hash(directory):
    """与C#实现完全一致的目录哈希计算"""
    md5 = hashlib.md5()
    
    # 生成排序的文件列表
    file_list = []
    for root, _, files in os.walk(directory):
        for file in files:
            full_path = os.path.join(root, file)
            rel_path = os.path.relpath(full_path, directory).lower()
            file_list.append((rel_path, full_path))
    
    # 按路径排序
    file_list.sort(key=lambda x: x[0])
    
    # 计算哈希
    for rel_path, full_path in file_list:
        # 文件名哈希
        md5.update(rel_path.encode('utf-8'))
        # 文件内容哈希
        with open(full_path, 'rb') as f:
            content_hash = hashlib.md5(f.read()).digest()
            md5.update(content_hash)
    
    return md5.hexdigest().lower()

def encrypt_hash(hash_str):
    """与C#实现完全一致的AES加密"""
    # 生成密钥
    sha256 = SHA256.new()
    sha256.update(b'cppuapa')
    key = sha256.digest()
    
    # 初始化加密器
    iv = b'\x00' * 16
    cipher = AES.new(key, AES.MODE_CBC, iv)
    
    # PKCS7填充
    data = hash_str.encode('utf-8')
    pad_len = AES.block_size - (len(data) % AES.block_size)
    data += bytes([pad_len] * pad_len)
    
    # 加密并编码
    encrypted = cipher.encrypt(data)
    return base64.b64encode(encrypted).decode('utf-8')

if __name__ == '__main__':
    main()
