#include "crypt.h"
#include "base64.h"

#ifdef _WINDOWS
#include <windows.h>
#include <bcrypt.h>

#pragma comment(lib, "bcrypt.lib")

#define NT_SUCCESS(Status)          (((NTSTATUS)(Status)) >= 0)
#define STATUS_UNSUCCESSFUL         ((NTSTATUS)0xC0000001L)
#else
#include <cstring>
#include <openssl/aes.h>
#include <openssl/evp.h>
#include <openssl/hmac.h>
#endif

int bytearray4_to_int(unsigned char* byte_array)
{
	return byte_array[0] + byte_array[1] * 256 + byte_array[2] * 65536 + byte_array[3] * 16777216;
}

int get_aes_keys_from_base64(const char* base64, AesKey* aesKey)
{
	int int32_size = 4;
	int keys_size = 56; // 32 байта (256 bit) ключ + 16 байт (128 bit) вектор +
	                    // длина ключа 4 байта + длина вектора 4 байта.
	unsigned char* pos;
	unsigned char* keysArray = new unsigned char[keys_size];

	if (!base64_decode(base64, keysArray, keys_size))
	{
		delete[] keysArray;
		return 0;
	}

	pos = keysArray;

	aesKey->KeySize = bytearray4_to_int(pos);
	pos += int32_size;

	aesKey->IVSize = bytearray4_to_int(pos);
	pos += int32_size;

	if (aesKey->KeySize + aesKey->IVSize > keys_size - int32_size * 2)
	{
		// Заданные размеры ключа и вектора больше максимально допустимых.
		delete[] keysArray;
		return 0;
	}

	aesKey->Key = new unsigned char[aesKey->KeySize];
	memcpy(aesKey->Key, pos, aesKey->KeySize);
	pos += aesKey->KeySize;

	aesKey->IV = new unsigned char[aesKey->IVSize];
	memcpy(aesKey->IV, pos, aesKey->IVSize);

	delete[] keysArray;
	return 1;
}

void dispose_aes_key(AesKey aesKey)
{
	delete[] aesKey.Key;
	delete[] aesKey.IV;
}

#ifdef _WINDOWS

int aes_decrypt(
	unsigned char* encrypted,
	int encryptedSize,
	AesKey aesKey,
	unsigned char** decrypted,
	int decryptedSize
)
{
	BCRYPT_ALG_HANDLE hAesAlg = NULL;
	BCRYPT_KEY_HANDLE hKey = NULL;
	PBYTE tempIV = NULL;
	DWORD tempIVLen = 0;
	NTSTATUS status = STATUS_UNSUCCESSFUL;
	PBYTE result = NULL;
	ULONG resultSize = 0;

	if (!NT_SUCCESS(status = BCryptOpenAlgorithmProvider(&hAesAlg, BCRYPT_AES_ALGORITHM, 0, NULL)))
	{
		goto cleanup;
	}

	if (!NT_SUCCESS(status = BCryptGenerateSymmetricKey(hAesAlg, &hKey, NULL, 0, aesKey.Key, aesKey.KeySize, 0)))
	{
		goto cleanup;
	}

	if (!NT_SUCCESS(status = BCryptSetProperty(
		hKey,
		BCRYPT_CHAINING_MODE,
		(PBYTE)BCRYPT_CHAIN_MODE_CBC,
		sizeof(BCRYPT_CHAIN_MODE_CBC),
		0)))
	{
		goto cleanup;
	}

	tempIV = (PBYTE)HeapAlloc(GetProcessHeap(), 0, aesKey.IVSize);
	if (tempIV == NULL)
	{
		status = STATUS_NO_MEMORY;
		goto cleanup;
	}

	tempIVLen = aesKey.IVSize;
	memcpy(tempIV, aesKey.IV, tempIVLen);

	result = (PBYTE)HeapAlloc(GetProcessHeap(), 0, decryptedSize);
	if (result == 0)
	{
		status = STATUS_NO_MEMORY;
		goto cleanup;
	}

	if (!NT_SUCCESS(status = BCryptDecrypt(
		hKey,
		encrypted,
		encryptedSize,
		NULL,
		tempIV,
		tempIVLen,
		result,
		decryptedSize,
		&resultSize,
		BCRYPT_BLOCK_PADDING
	)))
	{
		goto cleanup;
	}

	*decrypted = new unsigned char[decryptedSize];
	memcpy(*decrypted, result, decryptedSize);

cleanup:

	if (result != NULL)
		HeapFree(GetProcessHeap(), 0, result);
	if (tempIV != NULL)
		HeapFree(GetProcessHeap(), 0, tempIV);
	if (hKey != NULL)
		BCryptDestroyKey(hKey);
	if (hAesAlg != NULL)
		BCryptCloseAlgorithmProvider(hAesAlg, 0);

	return NT_SUCCESS(status);
}

int hmacsha256_sign(
	unsigned char* message,
	int messageSize,
	unsigned char* hmacKey,
	int hmacKeySize,
	unsigned char** hashOut,
	int* hashOutSize
) {
	BCRYPT_ALG_HANDLE algHandle = NULL;
	BCRYPT_HASH_HANDLE hashHandle = NULL;
	NTSTATUS status = STATUS_UNSUCCESSFUL;
	DWORD hashLength = 0, resultLength;
	unsigned char* hash = NULL;

	if (!NT_SUCCESS(status = BCryptOpenAlgorithmProvider(
		&algHandle,
		BCRYPT_SHA256_ALGORITHM,
		0,
		BCRYPT_ALG_HANDLE_HMAC_FLAG)))
	{
		goto cleanup;
	}

	if (!NT_SUCCESS(status = BCryptGetProperty(
		algHandle,
		BCRYPT_HASH_LENGTH,
		(PBYTE)&hashLength,
		sizeof(hashLength),
		&resultLength,
		0)))
	{
		goto cleanup;
	}

	hash = (unsigned char*)HeapAlloc(GetProcessHeap(), 0, hashLength);
	if (hash == NULL)
	{
		status = STATUS_NO_MEMORY;
		goto cleanup;
	}

	if (!NT_SUCCESS(status = BCryptCreateHash(
		algHandle,
		&hashHandle,
		NULL,
		0,
		hmacKey,
		hmacKeySize,
		0)))
	{
		goto cleanup;
	}

	if (hashHandle == NULL)
		goto cleanup;

	if (!NT_SUCCESS(status = BCryptHashData(hashHandle, message, messageSize, 0))) {
		goto cleanup;
	}

	if (!NT_SUCCESS(status = BCryptFinishHash(hashHandle, hash, hashLength, 0))) {
		goto cleanup;
	}

	*hashOutSize = hashLength;
	*hashOut = new unsigned char[hashLength];
	memcpy(*hashOut, hash, hashLength);

cleanup:

	if (hash != NULL)
		HeapFree(GetProcessHeap(), 0, hash);
	if (hashHandle != NULL)
		BCryptDestroyHash(hashHandle);
	if (algHandle != NULL)
		BCryptCloseAlgorithmProvider(algHandle, 0);

	return NT_SUCCESS(status);
}

#else

int aes_decrypt(
	unsigned char* encrypted,
	int encryptedSize,
	AesKey aesKey,
	unsigned char** decrypted,
	int decryptedSize
) {
	AES_KEY aesKeyOpenSSL;
    unsigned char *key, *iv;

    // Ключ в структуре AesKey после расшифровки "портится", поэтому необходимо сделать копию.
    key = new unsigned char[aesKey.KeySize];
    iv = new unsigned char[aesKey.IVSize];
    memcpy(key, aesKey.Key, aesKey.KeySize);
    memcpy(iv, aesKey.IV, aesKey.IVSize);

	*decrypted = new unsigned char[decryptedSize];
    AES_set_decrypt_key(key, 256, &aesKeyOpenSSL);
	AES_cbc_encrypt(encrypted, *decrypted, decryptedSize, &aesKeyOpenSSL, iv, AES_DECRYPT);

    delete[] key;
    delete[] iv;

    // Проверка корректности расшифровки. Просто проверяем, что первый символ сообщения "{" (начало JSON).
    // Да, "костыль", но для этой задачи достаточно, чтобы не усложнять код.
	if (*decrypted[0] != '{')
		return 0;

	return 1;
}

int hmacsha256_sign(
	unsigned char* message,
	int messageSize,
	unsigned char* hmacKey,
	int hmacKeySize,
	unsigned char** hashOut,
	int* hashOutSize
) {
    unsigned char hash[EVP_MAX_MD_SIZE];
    unsigned int hashSize;

    if (!HMAC(EVP_sha256(), hmacKey, hmacKeySize, message, messageSize, hash, &hashSize))
        return 0;

    *hashOut = new unsigned char[hashSize];
    memcpy(*hashOut, hash, hashSize);
    *hashOutSize = (int)hashSize;

    return 1;
}

#endif
