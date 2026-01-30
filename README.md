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
    for obj in *.o 2>/dev/null; do
        [ -e "$obj" ] && mv "$obj" "../all_objs/${libname}_${obj}"
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

## 📚 Referências

- [NIST NBIS Official](https://www.nist.gov/services-resources/software/nist-biometric-image-software-nbis)
- [WSQ Specification](https://www.fbi.gov/services/cjis/fingerprints-and-other-biometrics/biometric-specifications)
- [MSYS2 Documentation](https://www.msys2.org/docs/what-is-msys2/)

---
