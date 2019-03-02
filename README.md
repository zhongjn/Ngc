# 介绍

土制编译器前端，目前只支持C的一部分特性。后端采用LLVM。

目前只实现了简易的交互式编译运行，在窗口中粘贴一段包含main函数（必须是int main()）的C代码（以只包含#的一行结束），即可编译运行，输出main函数的返回值。

# Roadmap

将来可能支持（按优先级排列）：
- for、switch、continue语句
- 数组
- 指针
- 结构体
- ABI
- 宏
- 标准库


![construction](https://github.com/zhongjn/Ngc/blob/master/UnderConstruction.png)
