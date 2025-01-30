import numpy as np
import scipy.stats as stats

def compute_p(q):
    if q == 1:
        return 1.0 / 36
    else:
        return (1 - q) / (1 - q ** 36)

def compute_critical_values(p, q):
    # 验证总和是否为1
    if q == 1:
        total = p * 36
    else:
        total = p * (1 - q ** 36) / (1 - q)
    if not np.isclose(total, 1.0, atol=1e-9):
        raise ValueError("p 和 q 不满足概率总和为1的条件。")

    critical_values = []
    for k in range(1, 37):
        if q == 1:
            s_k = p * k
        else:
            s_k = p * (1 - q ** k) / (1 - q)
        # 计算对应的CDF值
        cdf = 0.5 + s_k / 2
        # 计算临界值 z_k
        z_k = stats.norm.ppf(cdf)
        critical_values.append(z_k)
    return critical_values

stage = '黄玄地天'
level = '九八七六五四三二一'

# 示例用法
if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--q', type=float, required=True)
    args = parser.parse_args()
    q = args.q
    p = compute_p(q)
    z_values = compute_critical_values(p, q)
    accum_r = 0
    for k, z_k in enumerate(z_values):
        accum_r += p * (q ** k)
        print(f"{stage[k//9]}阶{level[k%9]}品, 此级{p*(q**k)*100:>.2f}%, 至此{accum_r*100:>.2f}%, 强度上限{'无穷大' if np.isinf(z_k) else int(np.exp(z_k)*100)}%")
        pass