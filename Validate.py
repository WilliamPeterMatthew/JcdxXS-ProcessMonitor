import argparse
import os
import pyzipper
import hashlib
import shutil

# 定义常量
ZIP_PASSWORD = b'CPPUAPA'  # ZIP 文件的密码
AES_KEY = b'cppuapa'  # AES 加密的密钥
AES_IV = b'\x00' * 16  # 固定的 IV（16 字节全零）

def extract_zip(zip_path, extract_to):
    """解压带密码的 ZIP 文件"""
    with pyzipper.AESZipFile(zip_path, 'r', encryption=pyzipper.WZ_AES) as zf:
        zf.setpassword(ZIP_PASSWORD)
        zf.extractall(extract_to)
        print(f'解压完成，文件已提取到 {extract_to}')

def get_zip_comment(zip_path):
    """获取 ZIP 文件的注释"""
    with pyzipper.AESZipFile(zip_path, 'r', encryption=pyzipper.WZ_AES) as zf:
        zf.setpassword(ZIP_PASSWORD)
        return zf.comment.decode('utf-8')

def decrypt_hash(encrypted_hash):
    """解密 AES 加密的哈希值"""
    cipher = AES.new(AES_KEY, AES.MODE_CBC, AES_IV)
    decrypted = cipher.decrypt(bytes.fromhex(encrypted_hash))
    return decrypted.rstrip(b'\x00').decode('utf-8')

def calculate_directory_hash(directory):
    """计算目录的 MD5 哈希值"""
    md5 = hashlib.md5()
    for root, dirs, files in os.walk(directory):
        for file in sorted(files):
            file_path = os.path.join(root, file)
            with open(file_path, 'rb') as f:
                while chunk := f.read(8192):
                    md5.update(chunk)
    return md5.hexdigest()

def validate_package(zip_path, extract_to):
    """验证包的完整性"""
    # 获取 ZIP 文件的注释（包含加密的哈希值）
    encrypted_hash = get_zip_comment(zip_path)
    # 解密哈希值
    original_hash = decrypt_hash(encrypted_hash)
    # 计算解压后的目录的哈希值
    current_hash = calculate_directory_hash(extract_to)
    # 比较哈希值
    if original_hash == current_hash:
        print('文件未被篡改')
    else:
        print('文件已被篡改')

def main():
    # 创建命令行参数解析器
    parser = argparse.ArgumentParser(description='验证 PMR 文件的完整性')
    parser.add_argument('pmr_file', help='PMR 文件路径')
    parser.add_argument('-e', '--extract', help='解压目录路径')
    args = parser.parse_args()

    # 检查是否指定了 PMR 文件路径
    if not args.pmr_file:
        print('错误：未指定 PMR 文件路径。请使用 -h 或 --help 查看帮助信息。')
        exit(1)

    # 如果未指定解压目录，则使用临时目录
    if not args.extract:
        temp_dir = 'temp_extracted_files'
        os.makedirs(temp_dir, exist_ok=True)
    else:
        temp_dir = args.extract

    try:
        # 解压文件
        extract_zip(args.pmr_file, temp_dir)
        # 验证包的完整性
        validate_package(args.pmr_file, temp_dir)
    finally:
        # 如果使用了临时目录，则删除
        if not args.extract and os.path.exists(temp_dir):
            shutil.rmtree(temp_dir)
            print(f'临时解压目录 {temp_dir} 已删除')

if __name__ == '__main__':
    main()
