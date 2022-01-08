#ifndef __CRYPT_H__
#define __CRYPT_H__

typedef struct tagAesKey
{
    unsigned char* Key;
    unsigned char* IV;
    int KeySize;
    int IVSize;
} AesKey;

int aes_decrypt(
	unsigned char* encrypted,
	int encryptedSize,
	AesKey aesKey,
	unsigned char** decrypted,
	int decryptedSize
);
int hmacsha256_sign(
	unsigned char* message,
	int messageSize,
	unsigned char* hmacKey,
	int hmacKeySize,
	unsigned char** hashOut,
	int* hashOutSize
);
int get_aes_keys_from_base64(const char* base64, AesKey* aesKey);
void dispose_aes_key(AesKey aesKey);
int bytearray4_to_int(unsigned char* byte_array);

#endif //__CRYPT_H__
