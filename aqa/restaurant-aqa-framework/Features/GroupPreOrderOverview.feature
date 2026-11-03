@api @group-preorder @level1
Feature: Group pre-order overview

  Whoever books a table for several guests can invite the others to choose their own dishes
  before the visit. The owner of the reservation sees everybody who joined, what each
  person chose and what it all comes to. Nobody else gets to see that.

  Background:
    Given the restaurant has a waiter account
    And a restaurant with 2 dishes on the menu
    And a customer named Olivia who has booked a table for 4 guests

  Rule: The owner sees everyone who joined and what they chose

    @smoke @negative
    Scenario: A group that nobody has joined yet shows only its owner
      When Olivia views the group pre-order
      Then the group pre-order has 1 participant
      And the owner is listed first
      And the cart of Olivia is empty
      And the group total is zero

    Scenario: Guests who joined are listed after the owner
      Given the guest Mia has joined the group pre-order
      And the guest Noah has joined the group pre-order
      When Olivia views the group pre-order
      Then the group pre-order has 3 participants
      And the owner is listed first
      And the guest Mia is listed
      And the guest Noah is listed

    @negative
    Scenario: A guest who has not chosen anything yet has an empty cart
      Given the guest Mia has joined the group pre-order
      When Olivia views the group pre-order
      Then the cart of Mia is empty
      And Mia is shown as not submitted yet

    Scenario: Each guest's dishes and total are shown separately
      Given the guest Mia has joined the group pre-order
      And the guest Noah has joined the group pre-order
      And the guest Mia has put 2 portions of dish 1 in their cart
      And the guest Mia has put 1 portion of dish 2 in their cart
      And the guest Noah has put 1 portion of dish 2 in their cart
      When Olivia views the group pre-order
      Then the cart of Mia holds:
        | dish | portions |
        | 1    | 2        |
        | 2    | 1        |
      And the cart of Noah holds:
        | dish | portions |
        | 2    | 1        |

    Scenario: The group total adds up what every participant owes
      Given the guest Mia has joined the group pre-order
      And the guest Noah has joined the group pre-order
      And the guest Mia has put 2 portions of dish 1 in their cart
      And the guest Noah has put 1 portion of dish 2 in their cart
      When Olivia views the group pre-order
      Then the group total is the price of:
        | dish | portions |
        | 1    | 2        |
        | 2    | 1        |
      And the group total is the sum of what each participant owes

    Scenario: A guest is shown as submitted once they confirm their cart
      Given the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      When the guest Mia submits their cart
      And Olivia views the group pre-order
      Then Mia is shown as submitted

    Scenario: Putting the same dish in the cart twice adds up the portions
      Given the guest Mia has joined the group pre-order
      And the guest Mia has put 1 portion of dish 1 in their cart
      And the guest Mia has put 2 portions of dish 1 in their cart
      When Olivia views the group pre-order
      Then the cart of Mia holds:
        | dish | portions |
        | 1    | 3        |

  Rule: A group the owner has not opened yet has nobody in it

    Scenario: A group that was never opened has no participants and no total
      Given another customer named Peter who has booked a table for 4 guests and has not opened the group pre-order
      When Peter views the group pre-order of Peter
      Then the group pre-order has 0 participants
      And the group total is zero

  Rule: Everybody can see how full the group is

    Scenario: The status counts the owner and every guest who joined
      Given the guest Mia has joined the group pre-order
      When anyone checks how many people have joined
      Then the group status shows 2 of 4 participants
      And the group status is Open

  Rule: Only the owner of the reservation sees the group pre-order

    @negative
    Scenario: Another customer cannot see someone else's group pre-order
      Given another customer named Peter is signed in
      When Peter views the group pre-order
      Then they are turned away because only the owner of the reservation may do that

    @negative
    Scenario: A waiter cannot see the group pre-order through the owner's view
      When the waiter views the group pre-order
      Then they are turned away because only the owner of the reservation may do that

    @negative
    Scenario: A guest cannot see the group pre-order with the pass they joined with
      Given the guest Mia has joined the group pre-order
      When the guest Mia views the group pre-order
      Then they are turned away because they are not signed in

    @negative
    Scenario: Someone who is not signed in cannot see the group pre-order
      When someone who is not signed in views the group pre-order
      Then they are turned away because they are not signed in

    @negative
    Scenario: A group pre-order that does not exist cannot be viewed
      When Olivia views a group pre-order that does not exist
      Then they are told it could not be found
