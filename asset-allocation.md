# 资产分配合约

此合约用于资金的锁定与分配。
合约接受用户账户输入的资金，锁定一定时间。在锁定期间内任何人无法动用这笔资金。锁定结束后按照指定的分配规则将资金分发给其他账户。


## 角色

合约涉及的三个角色：

* 付款人：创建实例，输入资金
* 收款人：由付款人指定，在锁定结束后取得资金
* 评审人：可选角色，由付款人指定，并需收款人确认，有权决定最终分配的资金额。


## 流程

资金锁定及分配的流程如下：

```
生成分配实例  锁定资金       锁定期限         付款期限   收款期限
   |----------|--------------|--------------|---------|
   |  设定参数 |   资金锁定     |  付款账户确认  |         |
                             |       收款账户确认       |
```

整个流程分为4个阶段

1. 付款人生成实例并设定参数、转入资金。若设定评审人，则收款人需在此阶段确认评审人信息。此阶段付款人可取消实例。
2. 付款人调用**锁定**接口，资金被锁定在合约中，任何人将无法操作实例中的资金。
3. 锁定期结束后，进入资金分配阶段。此阶段有两个期限：付款期限和收款期限，二者前后关系没有强制要求。
   * 付款人需在付款期限前确认付款，若到期未付款，则默认视为确认。
   * 收款人需在收款期限前确认收款，若到期未收款，则视为放弃，付款人将可取回资金。
   * 若设置了评审人，则评审人需在此阶段确认最终分配的资金额度，即向收款人分配的资金占总资金的百分比，未分配的资金部分将退回给付款人。
   * 若设置了评审人，付款人可在付款期限前（包括资金锁定阶段）调用**中断**接口中断分配。中断后付款人和收款人将无法操作合约，由评审人决定资金的处理方式。
4. 资金分配完成或取消，实例结束。


## 接口

* 分配规则模版

```
CreateTemplate

Arguments: 
  rule

Event: Template(ID)
```

rule是一个整数数组，每个元素表示一个收款方所分得的比例，取值范围[0, 10000]，表示 0% ～ 100.00%


* 创建实例

```
CreateInstance

Arguments:
  template_id            模板ID
  payments               payment数组，payment是(payer, amount) 二元组
  payer_threshold        后续操作所需的最少付款人签名数量
  payees                 收款人列表，按照模版中的顺序匹配分配比例
  Reviewer (optional)    评审人，占用规则中的第一个分配项，收款人从第二项开始依次匹配

Event: Instance("create", instance_id)
```



* 输入资金

```
InputAsset

Arguments:
  instance_id  实例ID
  amount       资金数量
  payer        付款人

Event: Instance("input", instance_id, amount, payer)
```


* 锁定

```
Lock

Arguments:
  instance_id         实例ID
  lock_time           锁定期限
  payment_time        付款期限
  collection_time     收款期限
  Payer               付款人（数组）

Event: Instance("Lock", instance_id)
```


* 确认

各确认操作均使用此接口

```
Confirm

Arguments:
  instance_id  实例ID
  confirmer    确认者（数组）

Event: Instance("Confirm", instance_id, confirmer)
```


* 分配额

仅评审人调用

```
SetQuota

Arguments:
  instance_id     实例ID
  quota           [0-10000]范围内的整数，表示 0% ~ 100.00%
  reviewer        评审人

Event: Instance("Quota", instance_id, quota)
```

* 中断

```
Interrupt

Argument:
  instance_id  实例ID
  payer        付款人（数组）

Event: Instance("Interrupt", instance_id, payer)
```
