# OContabil

OContabil é uma solução avançada de processamento inteligente de documentos contábeis, desenvolvida para automatizar a extração e validação de dados de documentos financeiros como boletos bancários e notas fiscais. O sistema utiliza modelos de Inteligência Artificial de última geração (GLiNER 2) para realizar a extração de dados estruturados com alta precisão, mesmo em layouts variáveis.

## Principais Funcionalidades

### 1. Processamento Inteligente de Boletos
* Extração automática de dados críticos: linha digitável, valor, data de vencimento e CNPJ do beneficiário.
* Validação cruzada de dados bancários e verificações de integridade.
* Cálculo de métricas de confiança para garantir a precisão do lançamento.

### 2. Extração de Notas Fiscais (NF-e/NFS-e)
* Suporte a extração de campos como valor total, data de emissão, CNPJ de emitente/destinatário e chaves de acesso.
* Flexibilidade para lidar com diferentes padrões de notas municipais através de aprendizado Zero-Shot.

### 3. OCR e Processamento de Imagens
* Integração nativa com Tesseract OCR para leitura de documentos em PDF e formatos de imagem.
* Pré-processamento de imagens para otimizar a taxa de reconhecimento de caracteres.

### 4. Gerenciamento de Fila e Performance
* Processamento assíncrono em segundo plano para manter a interface de usuário sempre responsiva.
* Persistência de dados local utilizando SQLite para histórico e auditoria.

## Arquitetura Técnica

O projeto utiliza uma arquitetura híbrida para maximizar a performance e a precisão:

* **Interface de Usuário**: Desenvolvida em WPF com .NET 8, seguindo os padrões MVVM (CommunityToolkit.Mvvm) para uma arquitetura robusta e testável.
* **Motor de IA (GLiNER 2)**: Integração com o modelo `fastino/gliner2-base-v1` via ponte de integração Python (Python Bridge) ou inferência nativa ONNX.
* **Banco de Dados**: Entity Framework Core com SQLite para armazenamento leve e eficiente.
* **Métricas**: Sistema de diagnóstico integrado para monitoramento da saúde do modelo e logs de processamento.

## Requisitos do Sistema

### Ambiente C#/.NET
* .NET SDK 8.0 ou superior.
* Navegador moderno (WebView2 Runtime instalado).

### Ambiente Python (Motor de IA)
* Python 3.10 ou superior.
* Bibliotecas necessárias: `gliner2`, `torch`, `transformers`, `pymupdf`, `pytesseract`.

## Instalação e Configuração

1. Clone o repositório:
```bash
git clone https://github.com/vitorGgC569/OContabil.git
```

2. Configure o ambiente Python:
```bash
pip install gliner2 torch transformers pymupdf pytesseract Pillow
```

3. Configure o caminho do executável Python nas configurações do aplicativo (AppSettings.cs).

4. Compile e execute o projeto através do Visual Studio 2022 ou via CLI:
```bash
dotnet run --project OContabil
```

## Desenvolvimento e Roadmap

Este projeto está atualmente em fase de MVP (Produto Mínimo Viável), com foco em estabilidade e precisão de extração. Próximas etapas incluem:
* Aperfeiçoamento da inferência nativa ONNX para eliminar a dependência de runtime Python.
* Exportação direta para formatos contábeis (Excel, CSV, integrações API).
* Expansão da base de conhecimentos para documentos específicos do setor público.

---
**Autor:** Vitor Gabriel Gomes
**Projeto:** OContabil - Inteligência Contábil Estruturada