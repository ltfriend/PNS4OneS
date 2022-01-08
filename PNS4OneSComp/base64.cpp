#include <cstring>

int base64_isvalidchar(char c)
{
	if (c >= '0' && c <= '9')
		return 1;
	if (c >= 'A' && c <= 'Z')
		return 1;
	if (c >= 'a' && c <= 'z')
		return 1;
	if (c == '+' || c == '/' || c == '=')
		return 1;
	return 0;
}

size_t base64_decoded_size(const char* base64)
{
	size_t len, ret, i;

	if (base64 == NULL)
		return 0;

	len = strlen(base64);
	ret = len / 4 * 3;

	for (i = len; i-- > 0; )
	{
		if (base64[i] == '=')
			ret--;
		else
			break;
	}

	return ret;
}

int base64_decode(const char* base64, unsigned char* out, size_t size)
{
	size_t len, i, j;
	int v;
	int b64invs[] =
	{
		62, -1, -1, -1, 63, 52, 53, 54, 55, 56, 57, 58,
		59, 60, 61, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5,
		6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
		21, 22, 23, 24, 25, -1, -1, -1, -1, -1, -1, 26, 27, 28,
		29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42,
		43, 44, 45, 46, 47, 48, 49, 50, 51
	};

	if (base64 == NULL || out == NULL)
		return 0;

	len = strlen(base64);
	if (size < base64_decoded_size(base64) || len % 4 != 0)
		return 0;

	for (i = 0; i < len; i++) {
		if (!base64_isvalidchar(base64[i])) {
			return 0;
		}
	}

	for (i = 0, j = 0; i < len; i += 4, j += 3) {
		v = b64invs[base64[i] - 43];
		v = (v << 6) | b64invs[base64[i + 1] - 43];
		v = base64[i + 2] == '=' ? v << 6 : (v << 6) | b64invs[base64[i + 2] - 43];
		v = base64[i + 3] == '=' ? v << 6 : (v << 6) | b64invs[base64[i + 3] - 43];

		out[j] = (v >> 16) & 0xFF;
		if (base64[i + 2] != '=')
			out[j + 1] = (v >> 8) & 0xFF;
		if (base64[i + 3] != '=')
			out[j + 2] = v & 0xFF;
	}

	return 1;
}
