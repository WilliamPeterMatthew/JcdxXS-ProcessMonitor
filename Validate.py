import argparse
import os
import hashlib
import tempfile
import shutil
import pyzipper
from Crypto.Cipher import AES
from Crypto.Hash import SHA256
import base64

def main():
    parser = argparse.ArgumentParser(description='Validate a PMR file integrity.')
    parser.add_argument('pmr_file', help='Path to the PMR file to validate')
    parser.add_argument('-e', '--extract-dir', help='Directory to extract files (optional)')
    args = parser.parse_args()

    # 读取ZIP文件注释（加密哈希值）
    try:
        with pyzipper.AESZipFile(args.pmr_file, 'r') as zf:
            zf.setpassword(b'CPPUAPA')
            stored_hash = zf.comment.decode('utf-8')
    except Exception as e:
        print(f"❌ 文件读取失败: {str(e)}")
        return

    # 处理解压目录
    temp_dir = None
    extract_to = args.extract_dir
    if not extract_to:
        temp_dir = tempfile.mkdtemp()
        extract_to = temp_dir
    else:
        os.makedirs(extract_to, exist_ok=True)

    # 解压文件
    try:
        with pyzipper.AESZipFile(args.pmr_file, 'r') as zf:
            zf.setpassword(b'CPPUAPA')
            zf.extractall(path=extract_to)
    except Exception as e:
        print(f"❌ 解压失败: {str(e)}")
        if temp_dir:
            shutil.rmtree(temp_dir)
        return

    # 计算当前哈希
    try:
        current_hash = calculate_directory_hash(extract_to)
    except Exception as e:
        print(f"❌ 哈希计算失败: {str(e)}")
        if temp_dir:
            shutil.rmtree(temp_dir)
        return

    # 加密当前哈希
    try:
        encrypted_current = encrypt_hash(current_hash)
    except Exception as e:
        print(f"❌ 哈希加密失败: {str(e)}")
        if temp_dir:
            shutil.rmtree(temp_dir)
        return

    # 验证结果
    if encrypted_current == stored_hash:
        print("✅ 验证成功：文件完整未被篡改")
    else:
        print(f"❌ 验证失败：哈希值不匹配\n存储值: {stored_hash}\n计算值: {encrypted_current}")

    # 清理临时目录
    if temp_dir:
        shutil.rmtree(temp_dir)

def calculate_directory_hash(directory):
    """计算目录哈希（与C#实现一致）"""
    md5 = hashlib.md5()
    
    # 获取并排序文件列表（相对路径小写）
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
    """AES加密哈希值（与C#实现一致）"""
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
