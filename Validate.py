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

def main():
    # 参数解析
    parser = argparse.ArgumentParser(
        description='PMR文件完整性验证工具',
        add_help=False
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
    
    if len(sys.argv) == 1 or '-h' in sys.argv or '--help' in sys.argv:
        print("""用法：
  validate.py [pmr_file] [-e EXTRACT_DIR]
示例：
  validate.py Moni_111_222.pmr
  validate.py Moni_111_222.pmr -e ./extracted""")
        sys.exit(0)

    try:
        args = parser.parse_args()
        if not args.pmr_file:
            raise ValueError("必须指定PMR文件路径")
    except Exception as e:
        print(f"参数错误: {str(e)}", file=sys.stderr)
        sys.exit(1)

    try:
        # 读取ZIP注释
        with pyzipper.AESZipFile(args.pmr_file, 'r') as zf:
            zf.setpassword(b'CPPUAPA')
            stored_hash = zf.comment.decode('utf-8')

        # 处理解压目录
        temp_dir = None
        extract_to = args.extract_dir
        if not extract_to:
            temp_dir = tempfile.mkdtemp()
            extract_to = temp_dir
        else:
            os.makedirs(extract_to, exist_ok=True)

        # 解压文件
        with pyzipper.AESZipFile(args.pmr_file, 'r') as zf:
            zf.setpassword(b'CPPUAPA')
            zf.extractall(path=extract_to)

        # 计算哈希
        current_hash = calculate_directory_hash(extract_to)
        encrypted_current = encrypt_hash(current_hash)

        # 验证结果
        if encrypted_current == stored_hash:
            print("[SUCCESS] 验证通过")
        else:
            print("[FAIL] 哈希不匹配")
            print(f"存储值: {stored_hash}")
            print(f"计算值: {encrypted_current}")

    except Exception as e:
        print(f"验证失败: {str(e)}")
    finally:
        if temp_dir and os.path.exists(temp_dir):
            shutil.rmtree(temp_dir)

def calculate_directory_hash(directory):
    """修复1：严格匹配C#的排序规则"""
    md5 = hashlib.md5()
    
    # 修复点：使用原始大小写路径排序
    file_list = []
    for root, _, files in os.walk(directory):
        for file in files:
            full_path = os.path.join(root, file)
            # 修复点：保持原始大小写
            rel_path = os.path.relpath(full_path, directory)
            file_list.append((rel_path, full_path))
    
    # 修复点：按原始字符串排序（区分大小写）
    file_list.sort(key=lambda x: x[0])
    
    for rel_path, full_path in file_list:
        # 修复点：使用原始路径大小写
        md5.update(rel_path.encode('utf-8'))
        with open(full_path, 'rb') as f:
            # 修复点：分块计算以匹配C#的TransformBlock
            while chunk := f.read(4096):
                md5.update(hashlib.md5(chunk).digest())
    
    return md5.hexdigest().lower()

def encrypt_hash(hash_str):
    """修复2：严格对齐C#加密方式"""
    # 生成密钥
    sha256 = SHA256.new()
    sha256.update(b'cppuapa')
    key = sha256.digest()
    
    # 使用ECB模式替代CBC（因C#代码中IV为全零）
    cipher = AES.new(key, AES.MODE_ECB)
    
    # 填充处理
    data = hash_str.encode('utf-8')
    pad_len = AES.block_size - (len(data) % AES.block_size)
    data += bytes([pad_len] * pad_len)
    
    encrypted = cipher.encrypt(data)
    return base64.b64encode(encrypted).decode('utf-8')

if __name__ == '__main__':
    main()
