// 这个ts文件也代表着我对当前LLM回复格式的理解（而不是一个真正可执行的文件）
interface ChatBuffResult {
    response: string;
    buffTarget: "player" | "enemy";
    statType: "Attack" | "Defense" | "SpAttack" | "SpDefense" | "Speed";
    stages: -2 | -1 | 0 | 1 | 2;
    catchRateModifier: number;
  }

  interface AIActionResponse {
    moveIndex: number;
  }