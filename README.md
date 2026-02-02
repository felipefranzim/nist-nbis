# 🔧 Guia Completo: Compilar NBIS no Windows

Guia para compilar a biblioteca NIST NBIS no Windows e criar uma DLL para uso com C# .NET.

## 📋 Pré-requisitos

Você vai precisar instalar estas ferramentas antes de começar:

| Ferramenta | O que é | Download |
|------------|---------|----------|
| MSYS2 | Ambiente Unix-like para Windows | https://www.msys2.org/ |
| CMake | Ferramenta de build | https://cmake.org/download/ |
| Git | Controle de versão | https://git-scm.com/download/win |

---

## 📋 PASSO 1: Instalar o MSYS2

O MSYS2 é um ambiente que simula Linux no Windows, necessário porque o NBIS usa Makefiles Unix.

1) **Baixe o instalador em:** https://www.msys2.org/
   - Clique no link msys2-x86_64-xxxxxxxx.exe

2) **Execute o instalador**
   - Instale em C:\msys64 (caminho padrão)
   - **Importante:** Não use caminhos com espaços!

3) **Após instalar, abra o terminal MSYS2**
   - Vá em: Menu Iniciar → MSYS2 → MSYS2 MINGW64
   - ⚠️ Use especificamente o "MINGW64", não o "MSYS2 MSYS"!

4) **Atualize o MSYS2 (execute estes comandos no terminal que abriu):**

```bash
# Atualizar o sistema (pode pedir para fechar e reabrir)
pacman -Syu
```

Se o terminal fechar, abra novamente o `MSYS2 MINGW64` e continue:

```bash
# Continuar atualização
pacman -Su
```

5) **Instale as ferramentas de compilação:**

```bash
# Instalar GCC, Make e ferramentas necessárias
pacman -S --needed base-devel mingw-w64-x86_64-toolchain mingw-w64-x86_64-cmake git
```

Quando perguntar `Enter a selection (default=all):`, apenas pressione Enter para instalar tudo.

---

## 📋 PASSO 2: Baixar o Código Fonte do NBIS

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Criar uma pasta para o projeto
mkdir -p /c/projetos
cd /c/projetos

# Clonar o repositório do NBIS
git clone https://github.com/felipefranzim/nist-nbis.git

# Entrar na pasta
cd nist-nbis
```

---

## 📋 PASSO 3: Configurar o Build

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Criar pasta onde o NBIS será instalado
mkdir -p /c/nbis

# Configurar o build para Windows 64-bit
./setup.sh /c/nbis --MSYS --64
```

**O que esse comando faz:**
- `/c/nbis` = Pasta onde será instalado (equivale a C:\nbis no Windows)
- `--MSYS` = Indica que estamos usando MSYS/MinGW
- `--64` = Compilar para 64 bits

### 🔧 3.1 Corrigir TODOS os CMakeLists.txt

Ainda no terminal `MSYS2 MINGW64`, na pasta `nist-nbis` (raiz do repositório clonado):

```bash
find /c/projetos/nist-nbis -name "CMakeLists.txt" -exec sed -i 's/cmake_minimum_required(VERSION 2\.[0-9\.]*)/cmake_minimum_required(VERSION 3.5)/g' {} \;
```

E depois:

```bash
find /c/projetos/nist-nbis -name "CMakeLists.txt" -exec sed -i 's/CMAKE_MINIMUM_REQUIRED\s*(VERSION\s*[0-9]\.[0-9][0-9]*\.*[0-9]*)/cmake_minimum_required(VERSION 3.5)/gI' {} \;
```

### 🔧 3.2 Corrigir possível problema de variáveis globais duplicadas

Abra o seguinte arquivo com nano:

```bash
nano /c/projetos/nist-nbis/rules.mak
```

Procure a linha que define `CFLAGS` (provavelmente algo como):

```bash
CFLAGS = -O2 -w -ansi ... ... ...
```

Adicione `-fcommon` no final:

```bash
CFLAGS = -O2 -w -ansi ... ... ... -fcommon
```

Para salvar no nano: `Ctrl+O` → `Enter` → `Ctrl+X`

---

## 📋 PASSO 4: Compilar o NBIS

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Gerar arquivos de configuração
make config

# Compilar (pode demorar alguns minutos)
make it
```

---

## 📋 PASSO 5: Instalar o NBIS

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Instalar os binários e bibliotecas
make install LIBNBIS=yes
```

Após este comando, você terá em `C:\nbis`:

- `bin/` → Executáveis (cwsq.exe, dwsq.exe, etc.)
- `lib/` → Biblioteca estática (libnbis.a)
- `include/` → Headers (.h)

---

## 📋 PASSO 6: Corrigir a Biblioteca libnbis.a

A biblioteca `libnbis.a` gerada contém arquivos `.a` aninhados (bibliotecas dentro de bibliotecas), o que causa problemas no linking. Precisamos extrair todos os objetos `.o` e recriar a biblioteca corretamente.

```bash
cd /c/nbis/lib

# Criar pasta de trabalho
mkdir rebuild
cd rebuild

# Extrair as bibliotecas .a de dentro da libnbis.a
ar -x ../libnbis.a

# Criar pasta para todos os objetos
mkdir all_objs

# Extrair objetos de cada .a com prefixo único para evitar conflitos
for lib in *.a; do
    libname=$(basename "$lib" .a)
    echo "Processando: $lib"
    mkdir -p "temp_$libname"
    cd "temp_$libname"
    ar -x "../$lib"
    # Renomear cada .o com prefixo da biblioteca
    for obj in *.o; do
        mv "$obj" "../all_objs/${libname}_${obj}"
    done
    cd ..
    rm -rf "temp_$libname"
done

# Criar nova biblioteca com todos os objetos
ar rcs libnbis_fixed.a all_objs/*.o

# Criar índice de símbolos
ranlib libnbis_fixed.a

# Substituir a biblioteca original
cp libnbis_fixed.a ../libnbis.a

# Limpar
cd ..
rm -rf rebuild
```

---

## 📋 PASSO 7: Testar se Funcionou

```bash
# Testar o executável cwsq
/c/nbis/bin/cwsq

# Deve mostrar algo como:
# Usage: cwsq <r_bitrate> <output_ext> <image_file> [-r[awfile] w,h,d,[ppi]] [-o[utfile] outfile]
```

Se aparecer a mensagem de uso, o NBIS foi compilado com sucesso! 🎉

---

## 📋 PASSO 8: Criar a DLL para usar com C#

As bibliotecas compiladas são **estáticas** (.a). Para criar uma **DLL** que você pode usar com P/Invoke no C#, siga estes passos:

### 8.1 Criar o arquivo wrapper

Ainda no terminal MSYS2, crie uma pasta para o wrapper:

```bash
mkdir -p /c/projetos/wsq-dll
cd /c/projetos/wsq-dll
```

Crie o arquivo `nbis_wrapper.c`:

```bash
cat > nbis_wrapper.c << 'EOF'
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "wsq.h"
#include "nfiq.h"
#include "bozorth.h"
#include "lfs.h"

/* Variáveis globais requeridas pela libnbis */
int debug = 0;
int verbose = 0;

/* Variáveis globais requeridas pelo Bozorth3 */
int m1_xyt = 0;
int max_minutiae = DEFAULT_BOZORTH_MINUTIAE;
int min_computable_minutiae = MIN_COMPUTABLE_BOZORTH_MINUTIAE;
int verbose_main = 0;
int verbose_load = 0;
int verbose_bozorth = 0;
int verbose_threshold = 0;
FILE *errorfp = NULL;

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif

/* Inicialização - chamar uma vez antes de usar as funções */
static int initialized = 0;
static void ensure_initialized(void) {
    if (!initialized) {
        errorfp = stderr;
        initialized = 1;
    }
}

/* ========== WSQ Functions ========== */

EXPORT int wsq_encode_wrapper(
    unsigned char **odata,
    int *olen,
    const float r_bitrate,
    unsigned char *idata,
    const int w,
    const int h,
    const int d,
    const int ppi,
    char *comment_text)
{
    return wsq_encode_mem(odata, olen, r_bitrate, idata, w, h, d, ppi, comment_text);
}

EXPORT int wsq_decode_wrapper(
    unsigned char **odata,
    int *ow,
    int *oh,
    int *od,
    int *oppi,
    int *lossyflag,
    unsigned char *idata,
    const int ilen)
{
    return wsq_decode_mem(odata, ow, oh, od, oppi, lossyflag, idata, ilen);
}

EXPORT void wsq_free(unsigned char *data)
{
    if (data != NULL) {
        free(data);
    }
}

/* ========== NFIQ Functions ========== */

EXPORT int nfiq_from_wsq_data(
    int *nfiq_score,
    unsigned char *wsq_data,
    const int wsq_len)
{
    unsigned char *raw_data = NULL;
    int w, h, d, ppi, lossy;
    int ret;
    int score;
    float conf;
    int optflag;  /* Parâmetro adicional requerido por comp_nfiq */
    
    if (wsq_data == NULL || nfiq_score == NULL) {
        return -1;
    }
    
    /* Decodificar WSQ */
    ret = wsq_decode_mem(&raw_data, &w, &h, &d, &ppi, &lossy, wsq_data, wsq_len);
    if (ret != 0) {
        return -2;
    }
    
    /* Inicializar valores */
    score = 5;
    conf = 0.0f;
    optflag = 0;
    
    /* Calcular NFIQ - assinatura correta:
       comp_nfiq(int *nfiq, float *conf, unsigned char *idata,
                 const int w, const int h, const int d, const int ppi, int *optflag) */
    ret = comp_nfiq(&score, &conf, raw_data, w, h, d, ppi, &optflag);
    
    /* Copiar resultado */
    *nfiq_score = score;
    
    /* Liberar memória */
    free(raw_data);
    
    return ret;
}

EXPORT int nfiq_from_raw_data(
    int *nfiq_score,
    unsigned char *raw_data,
    const int w,
    const int h,
    const int ppi)
{
    int ret;
    int score;
    float conf;
    int optflag;
    
    if (raw_data == NULL || nfiq_score == NULL) {
        return -1;
    }
    
    /* Inicializar valores */
    score = 5;
    conf = 0.0f;
    optflag = 0;
    
    /* Calcular NFIQ */
    ret = comp_nfiq(&score, &conf, raw_data, w, h, 8, ppi, &optflag);
    
    /* Copiar resultado */
    *nfiq_score = score;
    
    return ret;
}

/* Helper para extrair info do BMP */
static int parse_bmp_grayscale(
    const unsigned char* bmpData,
    int bmpLen,
    unsigned char** out_pixels,
    int* out_w,
    int* out_h,
    int* out_ppi)
{
    if (!bmpData) return -1;
    if (bmpLen < 54) return -2;

    // BMP Header
    if (bmpData[0] != 0x42 || bmpData[1] != 0x4D) // 'B','M'
        return -3;

    int dataOffset = bmpData[10] | (bmpData[11] << 8) | (bmpData[12] << 16) | (bmpData[13] << 24);
    int w = bmpData[18] | (bmpData[19] << 8) | (bmpData[20] << 16) | (bmpData[21] << 24);
    int h = bmpData[22] | (bmpData[23] << 8) | (bmpData[24] << 16) | (bmpData[25] << 24);
    short bitsPerPixel = bmpData[28] | (bmpData[29] << 8);
    int xPelsPerMeter = bmpData[38] | (bmpData[39] << 8) | (bmpData[40] << 16) | (bmpData[41] << 24);
    int ppi = (int)(xPelsPerMeter / 39.3701);
    if (ppi < 100) ppi = 500;

    if (bitsPerPixel != 8 && bitsPerPixel != 24 && bitsPerPixel != 32)
        return -4; // só aceita 8,24,32bpp

    int bytesPerPixel = bitsPerPixel / 8;
    int stride = ((w * bytesPerPixel) + 3) & ~3;

    // Alocar saída
    unsigned char* pixels = (unsigned char*)malloc(w * h);
    if (!pixels) return -5;

    for (int y = 0; y < h; ++y) {
        int srcY = h - 1 - y; // BMP bottom-up
        int srcOffset = dataOffset + (srcY * stride);

        for (int x = 0; x < w; ++x) {
            int pixelOffset = srcOffset + (x * bytesPerPixel);
            unsigned char gray;
            if (bitsPerPixel == 8) {
                gray = bmpData[pixelOffset];
            } else {
                unsigned char b = bmpData[pixelOffset];
                unsigned char g = bmpData[pixelOffset+1];
                unsigned char r = bmpData[pixelOffset+2];
                gray = (unsigned char)(0.299*r + 0.587*g + 0.114*b);
            }
            pixels[y * w + x] = gray;
        }
    }
    *out_pixels = pixels;
    *out_w = w;
    *out_h = h;
    *out_ppi = ppi;
    return 0;
}

/* Wrapper que aceita BMP em memória */
EXPORT int nfiq_from_bmp_data(
    int* nfiq_score,
    const unsigned char* bmpData,
    int bmpLen)
{
    unsigned char* raw_pixels = NULL;
    int w = 0, h = 0, ppi = 500;
    int ret = parse_bmp_grayscale(bmpData, bmpLen, &raw_pixels, &w, &h, &ppi);
    if (ret != 0) {
        if (raw_pixels) free(raw_pixels);
        return ret;
    }

    int score;
    float conf;
    int optflag = 0;
    ret = comp_nfiq(&score, &conf, raw_pixels, w, h, 8, ppi, &optflag);

    if (raw_pixels) free(raw_pixels);
    *nfiq_score = score;
    return ret;
}

/* ========== MINUTIAE EXTRACTION (mindtct) ========== */

/* Converte PPI para pixels per mm */
static double ppi_to_ppmm(int ppi)
{
    return (double)ppi / 25.4;
}

/* Extrai minúcias de uma imagem raw grayscale */
EXPORT int extract_minutiae(
    unsigned char *raw_data,
    const int w,
    const int h,
    const int ppi,
    int *minutiae_count,
    int *x_coords,
    int *y_coords,
    int *thetas,
    int *qualities,
    const int max_minutiae)
{
    int ret;
    MINUTIAE *minutiae = NULL;
    int *direction_map = NULL;
    int *low_contrast_map = NULL;
    int *low_flow_map = NULL;
    int *high_curve_map = NULL;
    int *quality_map = NULL;
    int map_w, map_h;
    unsigned char *binarized_image = NULL;
    int bw, bh, bd;
    double ppmm;
    int i, ox, oy, ot;
    int count;
    
    if (raw_data == NULL || minutiae_count == NULL) {
        return -1;
    }
    
    /* Converter PPI para pixels per mm */
    ppmm = ppi_to_ppmm(ppi);
    
    /* Chamar get_minutiae com a assinatura correta (18 parametros) */
    ret = get_minutiae(&minutiae, &quality_map, &direction_map,
                       &low_contrast_map, &low_flow_map, &high_curve_map,
                       &map_w, &map_h,
                       &binarized_image, &bw, &bh, &bd,
                       raw_data, w, h, 8, ppmm, &lfsparms_V2);
    
    if (ret != 0) {
        return ret;
    }
    
    /* Copiar minúcias para os arrays de saída */
    count = (minutiae->num < max_minutiae) ? minutiae->num : max_minutiae;
    *minutiae_count = count;
    
    for (i = 0; i < count; i++) {
        /* Converter para representação NIST internal (usada pelo bozorth3) */
        lfs2nist_minutia_XYT(&ox, &oy, &ot, minutiae->list[i], w, h);
        
        x_coords[i] = ox;
        y_coords[i] = oy;
        thetas[i] = ot;
        
        /* Reliability é um double entre 0.0 e 1.0, convertemos para 0-100 */
        qualities[i] = (int)(minutiae->list[i]->reliability * 100.0);
    }
    
    /* Liberar memória */
    free_minutiae(minutiae);
    if (direction_map) free(direction_map);
    if (low_contrast_map) free(low_contrast_map);
    if (low_flow_map) free(low_flow_map);
    if (high_curve_map) free(high_curve_map);
    if (quality_map) free(quality_map);
    if (binarized_image) free(binarized_image);
    
    return 0;
}

/* Extrai minúcias de dados WSQ */
EXPORT int extract_minutiae_from_wsq(
    unsigned char *wsq_data,
    const int wsq_len,
    int *minutiae_count,
    int *x_coords,
    int *y_coords,
    int *thetas,
    int *qualities,
    const int max_minutiae)
{
    unsigned char *raw_data = NULL;
    int w, h, d, ppi, lossy;
    int ret;
    
    if (wsq_data == NULL || minutiae_count == NULL) {
        return -1;
    }
    
    /* Decodificar WSQ */
    ret = wsq_decode_mem(&raw_data, &w, &h, &d, &ppi, &lossy, wsq_data, wsq_len);
    if (ret != 0) {
        return -2;
    }
    
    /* Extrair minúcias */
    ret = extract_minutiae(raw_data, w, h, ppi,
                           minutiae_count, x_coords, y_coords, thetas, qualities,
                           max_minutiae);
    
    free(raw_data);
    return ret;
}

/* ========== BOZORTH3 MATCHING ========== */

/* 
 * Compara duas digitais usando arrays de minúcias
 * Retorna o match score (quanto maior, mais similar)
 */
EXPORT int bozorth_match(
    const int probe_count,
    const int *probe_x,
    const int *probe_y,
    const int *probe_theta,
    const int gallery_count,
    const int *gallery_x,
    const int *gallery_y,
    const int *gallery_theta)
{
    struct xyt_struct probe_xyt;
    struct xyt_struct gallery_xyt;
    int i;
    int match_score;
    
    if (probe_count <= 0 || gallery_count <= 0) {
        return 0;
    }
    
    /* Limitar ao máximo permitido */
    int p_count = (probe_count > MAX_BOZORTH_MINUTIAE) ? MAX_BOZORTH_MINUTIAE : probe_count;
    int g_count = (gallery_count > MAX_BOZORTH_MINUTIAE) ? MAX_BOZORTH_MINUTIAE : gallery_count;
    
    /* Preencher estrutura probe */
    probe_xyt.nrows = p_count;
    for (i = 0; i < p_count; i++) {
        probe_xyt.xcol[i] = probe_x[i];
        probe_xyt.ycol[i] = probe_y[i];
        probe_xyt.thetacol[i] = probe_theta[i];
    }
    
    /* Preencher estrutura gallery */
    gallery_xyt.nrows = g_count;
    for (i = 0; i < g_count; i++) {
        gallery_xyt.xcol[i] = gallery_x[i];
        gallery_xyt.ycol[i] = gallery_y[i];
        gallery_xyt.thetacol[i] = gallery_theta[i];
    }
    
    /* Executar matching */
    match_score = bozorth_main(&probe_xyt, &gallery_xyt);
    
    return match_score;
}

/*
 * Versão simplificada: extrai minúcias e faz matching em uma única chamada
 * probe_data e gallery_data são imagens raw grayscale
 */
EXPORT int match_fingerprints_raw(
    int *match_score,
    unsigned char *probe_data,
    const int probe_w,
    const int probe_h,
    const int probe_ppi,
    unsigned char *gallery_data,
    const int gallery_w,
    const int gallery_h,
    const int gallery_ppi)
{
    int ret;
    int probe_count, gallery_count;
    int probe_x[MAX_BOZORTH_MINUTIAE], probe_y[MAX_BOZORTH_MINUTIAE], probe_theta[MAX_BOZORTH_MINUTIAE], probe_quality[MAX_BOZORTH_MINUTIAE];
    int gallery_x[MAX_BOZORTH_MINUTIAE], gallery_y[MAX_BOZORTH_MINUTIAE], gallery_theta[MAX_BOZORTH_MINUTIAE], gallery_quality[MAX_BOZORTH_MINUTIAE];
    
    if (match_score == NULL || probe_data == NULL || gallery_data == NULL) {
        return -1;
    }
    
    /* Extrair minúcias da probe */
    ret = extract_minutiae(probe_data, probe_w, probe_h, probe_ppi,
                           &probe_count, probe_x, probe_y, probe_theta, probe_quality,
                           MAX_BOZORTH_MINUTIAE);
    if (ret != 0) {
        return -2;
    }
    
    /* Extrair minúcias da gallery */
    ret = extract_minutiae(gallery_data, gallery_w, gallery_h, gallery_ppi,
                           &gallery_count, gallery_x, gallery_y, gallery_theta, gallery_quality,
                           MAX_BOZORTH_MINUTIAE);
    if (ret != 0) {
        return -3;
    }
    
    /* Fazer matching */
    *match_score = bozorth_match(probe_count, probe_x, probe_y, probe_theta,
                                  gallery_count, gallery_x, gallery_y, gallery_theta);
    
    return 0;
}

/*
 * Match de duas imagens WSQ
 */
EXPORT int match_fingerprints_wsq(
    int *match_score,
    unsigned char *probe_wsq,
    const int probe_wsq_len,
    unsigned char *gallery_wsq,
    const int gallery_wsq_len)
{
    unsigned char *probe_raw = NULL;
    unsigned char *gallery_raw = NULL;
    int probe_w, probe_h, probe_d, probe_ppi, probe_lossy;
    int gallery_w, gallery_h, gallery_d, gallery_ppi, gallery_lossy;
    int ret;
    
    if (match_score == NULL || probe_wsq == NULL || gallery_wsq == NULL) {
        return -1;
    }
    
    /* Decodificar probe WSQ */
    ret = wsq_decode_mem(&probe_raw, &probe_w, &probe_h, &probe_d, &probe_ppi, &probe_lossy, probe_wsq, probe_wsq_len);
    if (ret != 0) {
        return -2;
    }
    
    /* Decodificar gallery WSQ */
    ret = wsq_decode_mem(&gallery_raw, &gallery_w, &gallery_h, &gallery_d, &gallery_ppi, &gallery_lossy, gallery_wsq, gallery_wsq_len);
    if (ret != 0) {
        free(probe_raw);
        return -3;
    }
    
    /* Fazer matching */
    ret = match_fingerprints_raw(match_score,
                                  probe_raw, probe_w, probe_h, probe_ppi,
                                  gallery_raw, gallery_w, gallery_h, gallery_ppi);
    
    free(probe_raw);
    free(gallery_raw);
    
    return ret;
}
EOF
```

### 8.2 Criar um header de compatibilidade sys/times.h

Pode ser que ao tentar compilar a DLL depois, ocorra erro no header sys/times.h, que é específico de Unix/Linux e não existe no MinGW Windows. Vamos contornar isso.

```
cd /c/nbis/include

# Criar a pasta sys se não existir
mkdir -p sys

# Criar um times.h vazio/stub
cat > sys/times.h << 'EOF'
/* Stub for Windows compatibility */
#ifndef _SYS_TIMES_H
#define _SYS_TIMES_H

#include <time.h>

struct tms {
    clock_t tms_utime;
    clock_t tms_stime;
    clock_t tms_cutime;
    clock_t tms_cstime;
};

static inline clock_t times(struct tms *buf) {
    if (buf) {
        buf->tms_utime = clock();
        buf->tms_stime = 0;
        buf->tms_cutime = 0;
        buf->tms_cstime = 0;
    }
    return clock();
}

#endif /* _SYS_TIMES_H */
EOF
```

Se precisar compilar a dll x86 posteriormente, copie esse header para a pasta em que estiver trabalhando na versão x86.

```
cd /c/nbis32/include
mkdir -p sys
cp /c/nbis/include/sys/times.h sys/
```

### 8.2 Compilar a DLL

```bash
gcc -shared -o nbis_wrapper.dll nbis_wrapper.c \
    -I/c/nbis/include \
    -L/c/nbis/lib \
    -lnbis \
    -lm \
    -static-libgcc
```

Se tudo der certo, você terá o arquivo `nbis_wrapper.dll` em `C:\projetos\wsq-dll\`.

### 8.3 Verificar a DLL

```bash
# Verificar se a DLL foi criada
ls -la nbis_wrapper.dll

# Verificar as funções exportadas
nm wsq_nifq_wrapper.dll | grep wsq
```

---


## 📋 PASSO 9: Se quiser também criar uma DLL x86

1) Abra o MINGW32 localizado em `C:\msys64\mingw32.exe`
2) A partir de agora, todos os comandos deverão ser feitos no terminal do MINGW32 para que a compilação x86 seja realizada

### 9.1: Instalar o toolchain x86
```
pacman -S --needed base-devel mingw-w64-i686-toolchain mingw-w64-i686-cmake git
```

### 9.2: Limpar e recombilar a NBIS

É recomendado que se faça o procedimento de forma limpa, ou seja, clone o repositório novamente e faça o processo do 0 para evitar arquivos buildados anteriormente para x64.

```
cd /c/projetos/nist-nbis

# Limpar build anterior
make clean

# Criar pasta separada para 32-bit
mkdir -p /c/nbis32

# Reconfigurar para 32-bit
./setup.sh /c/nbis32 --MSYS --32

# Aplicar correção do -fcommon no rules.mak
nano /c/projetos/nist-nbis/rules.mak
# Adicionar -fcommon no CFLAGS

# Compilar
make config
make it

# Instalar
make install LIBNBIS=yes
```

### 9.3: Corrigir a libnbis.a (32-bit)

```
cd /c/nbis32/lib

mkdir rebuild && cd rebuild

ar -x ../libnbis.a

mkdir all_objs

for lib in *.a; do libname=$(basename "$lib" .a); echo "Processando: $lib"; mkdir -p "temp_$libname"; cd "temp_$libname"; ar -x "../$lib"; for obj in *.o; do [ -e "$obj" ] && mv "$obj" "../all_objs/${libname}_${obj}"; done; cd ..; rm -rf "temp_$libname"; done

# Criar nova biblioteca
ar rcs libnbis_fixed.a all_objs/*.o

# Criar índice
ranlib libnbis_fixed.a

# Substituir original
cp libnbis_fixed.a ../libnbis.a

# Limpar
cd ..

rm -rf rebuild
```

### 9.4: Compilar a DLL 32-bit

```
cd /c/projetos/wsq-dll

gcc -shared -o wsq_wrapper_x86.dll wsq_wrapper.c \
    -I/c/nbis32/include \
    -L/c/nbis32/lib \
    -lnbis \
    -lm \
    -static-libgcc
```

--- 
## 📋 PASSO 10: Usar a DLL no C#

Copie a `nbis_wrapper.dll` para a pasta do seu projeto C# e use este código:

```csharp
using System;
using System.Runtime.InteropServices;

namespace WsqSharp.Native
{
    public static class WsqNative
    {
        private const string DLL_NAME = "wsq_wrapper.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int wsq_encode_wrapper(
            out IntPtr odata,
            out int olen,
            float r_bitrate,
            byte[] idata,
            int w,
            int h,
            int d,
            int ppi,
            string comment_text);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int wsq_decode_wrapper(
            out IntPtr odata,
            out int ow,
            out int oh,
            out int od,
            out int oppi,
            out int lossyflag,
            byte[] idata,
            int ilen);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void wsq_free(IntPtr data);
    }
}
```

### Exemplo de uso completo em C#:

```csharp
using System;
using System.Runtime.InteropServices;

public class WsqConverter
{
    /// <summary>
    /// Converte uma imagem raw (grayscale 8-bit) para WSQ
    /// </summary>
    /// <param name="rawImageData">Dados da imagem em grayscale 8-bit</param>
    /// <param name="width">Largura da imagem</param>
    /// <param name="height">Altura da imagem</param>
    /// <param name="ppi">Resolução em pixels por polegada (geralmente 500 para impressões digitais)</param>
    /// <param name="bitrate">Taxa de compressão (0.75 é um bom padrão para impressões digitais)</param>
    /// <returns>Dados comprimidos em formato WSQ</returns>
    public static byte[] EncodeToWsq(byte[] rawImageData, int width, int height, int ppi = 500, float bitrate = 0.75f)
    {
        IntPtr outputPtr;
        int outputLen;
        
        int result = WsqNative.wsq_encode_wrapper(
            out outputPtr,
            out outputLen,
            bitrate,
            rawImageData,
            width,
            height,
            8,  // depth = 8 bits
            ppi,
            null);
        
        if (result != 0)
            throw new Exception($"Erro ao codificar WSQ: {result}");
        
        try
        {
            byte[] wsqData = new byte[outputLen];
            Marshal.Copy(outputPtr, wsqData, 0, outputLen);
            return wsqData;
        }
        finally
        {
            WsqNative.wsq_free(outputPtr);
        }
    }

    /// <summary>
    /// Decodifica uma imagem WSQ para raw grayscale
    /// </summary>
    public static (byte[] data, int width, int height, int ppi) DecodeFromWsq(byte[] wsqData)
    {
        IntPtr outputPtr;
        int width, height, depth, ppi, lossyFlag;
        
        int result = WsqNative.wsq_decode_wrapper(
            out outputPtr,
            out width,
            out height,
            out depth,
            out ppi,
            out lossyFlag,
            wsqData,
            wsqData.Length);
        
        if (result != 0)
            throw new Exception($"Erro ao decodificar WSQ: {result}");
        
        try
        {
            byte[] rawData = new byte[width * height];
            Marshal.Copy(outputPtr, rawData, 0, rawData.Length);
            return (rawData, width, height, ppi);
        }
        finally
        {
            WsqNative.wsq_free(outputPtr);
        }
    }
}
```

---

## 🔧 Solução de Problemas

### Erro: "archive has no index; run ranlib to add one"

A biblioteca foi criada com estrutura aninhada. Execute o PASSO 6 novamente para corrigir.

### Erro: "undefined reference to `debug'"

Certifique-se de que o arquivo `wsq_wrapper.c` contém a linha:
```c
int debug = 0;
```

### Erro: "cannot find -lwsq" ou similares

O NBIS gera uma única biblioteca `libnbis.a` em vez de bibliotecas separadas. Use `-lnbis` em vez de `-lwsq -ljpegl ...`

### A DLL não carrega no C#

- Certifique-se de que a DLL está na mesma pasta do executável ou no PATH
- Use a versão 64-bit da DLL com aplicações 64-bit (e vice-versa)
- Verifique se todas as dependências estão presentes

---

## 📚 Referências

- [NIST NBIS Official](https://www.nist.gov/services-resources/software/nist-biometric-image-software-nbis)
- [WSQ Specification](https://www.fbi.gov/services/cjis/fingerprints-and-other-biometrics/biometric-specifications)
- [MSYS2 Documentation](https://www.msys2.org/docs/what-is-msys2/)

---
