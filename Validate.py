import os
import argparse
import sys
import hashlib
import pyzipper
from Crypto.Cipher import AES
from Crypto.Hash import SHA256
import base64

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('pmr_file')
    parser.add_argument('-e', '--extract-dir')
    args = parser.parse_args()

    # 读取存储的哈希值
    with pyzipper.AESZipFile(args.pmr_file, 'r') as zf:
        zf.setpassword(b'CPPUAPA')
        stored_hash = zf.comment.decode()
    
    # 解压文件（略）
    
    # 关键修正：完全对齐C#的哈希计算逻辑
    def calculate_directory_hash(directory):
        md5 = hashlib.md5()
        
        # 1. 获取规范化的文件列表
        files = []
        for root, _, filenames in os.walk(directory):
            for f in filenames:
                full_path = os.path.join(root, f)
                rel_path = os.path.relpath(full_path, directory)
                # Windows路径风格 + 小写
                rel_path = rel_path.replace('/', '\\').lower()  
                files.append((rel_path, full_path))
        
        # 2. 严格按C#的排序规则
        files.sort(key=lambda x: x[0].lower())
        
        # 3. 精确模拟C#的哈希流程
        for rel_path, full_path in files:
            # 文件名部分（UTF-8小写）
            path_bytes = rel_path.encode('utf-8')
            md5.update(path_bytes)
            
            # 文件内容部分（完整文件MD5）
            with open(full_path, 'rb') as f:
                file_md5 = hashlib.md5(f.read()).digest()
                md5.update(file_md5)
        
        return md5.hexdigest().lower()

    # 关键修正：精确复制C#加密流程
    def encrypt_hash(hash_str):
        key = SHA256.new(b'cppuapa').digest()
        iv = b'\x00'*16
        cipher = AES.new(key, AES.MODE_CBC, iv)
        
        # PKCS7填充
        data = hash_str.encode('utf-8')
        pad_len = 16 - (len(data) % 16)
        data += bytes([pad_len]*pad_len)
        
        encrypted = cipher.encrypt(data)
        return base64.b64encode(encrypted).decode()

    # 验证流程
    current_hash = calculate_directory_hash(extract_dir)
    encrypted_current = encrypt_hash(current_hash)
    
    print(f"Stored: {stored_hash}")
    print(f"Actual: {encrypted_current}")
    print("验证结果:", "通过" if stored_hash == encrypted_current else "失败")

if __name__ == '__main__':
    main()
