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
    """完全对齐C#实现的哈希计算"""
    md5 = hashlib.md5()
    
    # 修复1：严格模拟C#的路径处理逻辑
    file_list = []
    for root, _, files in os.walk(directory):
        # 修复2：过滤隐藏文件（与C#行为一致）
        files = [f for f in files if not f.startswith('.')]
        for file in files:
            full_path = os.path.join(root, file)
            # 修复3：使用与C#完全相同的相对路径计算方式
            rel_path = os.path.relpath(full_path, directory)
            # 修复4：强制转换为小写并统一使用Windows路径分隔符
            rel_path = rel_path.replace(os.sep, '\\').lower()
            file_list.append((rel_path, full_path))
    
    # 修复5：严格按小写路径排序（模拟C#的OrderBy）
    file_list.sort(key=lambda x: x[0])
    
    for rel_path, full_path in file_list:
        # 修复6：精确模拟C#的TransformBlock调用
        md5.update(rel_path.encode('utf-8'))
        
        # 修复7：完全复制C#的文件内容哈希方式
        with open(full_path, 'rb') as f:
            file_content = f.read()
            file_hash = hashlib.md5(file_content).digest()
            md5.update(file_hash)
    
    return md5.hexdigest().lower()

def encrypt_hash(hash_str):
    """精确复制C#加密流程"""
    # 修复8：使用正确的密钥派生方式
    key = SHA256.new(b'cppuapa').digest()
    
    # 修复9：恢复CBC模式+零IV
    iv = b'\x00' * 16
    cipher = AES.new(key, AES.MODE_CBC, iv)
    
    # 修复10：精确填充处理
    data = hash_str.encode('utf-8')
    pad_len = AES.block_size - (len(data) % AES.block_size)
    data += bytes([pad_len] * pad_len)
    
    encrypted = cipher.encrypt(data)
    return base64.b64encode(encrypted).decode('utf-8')

if __name__ == '__main__':
    main()
