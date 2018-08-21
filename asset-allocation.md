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
生成分配实例  锁定资金        锁定期限
   |----------|--------------|-------------
   |  设定参数 |   资金锁定     |  确认，触发分配
```

整个流程分为3个阶段

1. 付款人生成实例并设定参数、转入资金。若设定评审人，则收款人需在此阶段确认评审人信息。此阶段付款人可取消实例。
2. 付款人调用**锁定**接口，资金被锁定在合约中，任何人将无法操作实例中的资金。
3. 锁定期结束后，进入资金分配阶段。此阶段收付款双方任意一方确认，即可完成资金的分配，实例结束。
   * 若设置了评审人，则评审人需在收付款方确认前确定最终向收款人分配的资金额度，未分配的资金部分将退回给付款人。
   * 若设置了评审人，付款人可调用**退款**接口申请退款。需收款人或评审人也调用退款接口确认同意退款后，资金才能够退回。



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

* 收款人参数设定

```
PayeeParam

Arguments:
  payee_threshold      最少收款人签名数量
  payee                收款人，需满足门限数量

Event: Instance("PayeeParam", instance_id, payee_threshold)
```


* 输入资金

在设置参数阶段，付款人通过此接口可以多次向实例中补充资金。

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

仅评审人调用，决定收款人实际获得的金额相对预期的占比。未分配给收款人的资金将退回给付款人。

```
SetQuota

Arguments:
  instance_id     实例ID
  quota           [0-10000]范围内的整数，表示0% ~ 100.00%
  reviewer        评审人

Event: Instance("Quota", instance_id, quota)
```

* 退款

```
Refund

Argument:
  instance_id  实例ID
  operator     操作人

Event: Instance("Refund", instance_id, operator)
```
