int slowfib(int n) {
    if (n == 0) return 0;
    if (n == 1) return 1;
    return slowfib(n - 1) + slowfib(n - 2);
}

int main() {
    return slowfib(30);
}