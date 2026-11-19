# Third-Party Notices

本项目代码以 **AGPL-3.0-or-later** 发布。引入的第三方代码库与数据词典须与 AGPL-3.0 兼容，并在此逐项登记：名称、来源、协议、用途、是否修改。

> 引入新依赖前必须先核对 LICENSE；协议不清或不兼容的件不得进入仓库。

## 一、代码库（编译/链接进程序）

| 组件 | 用途 | 协议 | 与 AGPL-3.0 兼容性 | 状态 |
|---|---|---|---|---|
| Microsoft.Data.Sqlite | 本地 SQLite 存储 | MIT（.NET 库） | 兼容 | 规划引入，未落地 |
| PinyinM.NET | 汉字转拼音、拼音模糊搜索 | MIT | 兼容 | 待评估 |
| PinInCS | 拼音串→词条匹配（NFA/索引） | 待核实仓库 LICENSE | 待确认 | 未引入，核实前禁用 |
| ToolGood.Words | 拼音首字母/全拼、繁简转换 | 待核实仓库 LICENSE | 待确认 | 未引入，核实前禁用 |
| llama.cpp（后期） | 本地小模型推理 | MIT | 兼容 | 后期 AI 层评估 |
| ONNX Runtime（后期） | 本地小模型推理 | MIT | 兼容 | 后期 AI 层评估 |
| librime（后备） | 输入法引擎参考/后备底座 | BSD-3-Clause | 兼容 | 未引入 |

## 二、数据词典（随安装包分发，协议单独声明）

| 数据 | 内容 | 协议 | 义务 | 状态 |
|---|---|---|---|---|
| drkameleon/complete-hsk-vocabulary | 11470 词条 + 数字声调拼音 + 频率，已转为 `data/dict/hsk-basic.tsv` | MIT | 保留版权与 MIT 声明（见 `data/dict/LICENSE.complete-hsk-vocabulary.txt`） | **已引入** |
| fxsjy/jieba | 常用词表 + 词频（词表部分），取词频 ≥100、1–4 字，已转为 `data/dict/common-mit.tsv` | MIT | 保留版权与 MIT 声明（见 `data/dict/LICENSE.jieba.txt`） | **已引入** |
| mozillazg/pinyin-data | 字→拼音、多音字映射，作为通用词表注音底座 | MIT | 保留署名与 MIT 声明（见 `data/dict/LICENSE.pinyin-data.txt`） | **已引入** |
| CC-CEDICT | 词条 + 拼音（英文释义可弃） | CC BY-SA 4.0 | 署名；衍生词典同 BY-SA 开放 | 待评估，纳入须单独标注 |
| rime-ice 雾凇拼音 | 简体词库 + 词频 | GPL-3.0 | 衍生词库须 GPL-3.0 | 原型备选，注意与 AGPL 边界 |
| rime-essay 八股文 | 预设词汇表/语言模型 | LGPL-3 | 修改按 LGPL 回流 | 备选 |

## 三、协议兼容性备忘

- **可直接与 AGPL-3.0 组合**：MIT、BSD-2/3-Clause、Apache-2.0、LGPL-3.0、GPL-3.0。
- **数据 copyleft 与代码 copyleft 分开算**：CC BY-SA 仅约束衍生数据，不传染代码。
- 每个分发副本须附带本文件与各上游 LICENSE 原文（见 `data/third_party/`）。
